using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.Net.Specification.HttpApi;
using static OpenSearch.Net.HttpMethod;

// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

public class OpenSearch(IConfiguration configuration, ILogger<OpenSearch> logger)
{
    public Uri OpenSearchUrl()
    {
        var openSearchUrl = configuration.GetValue<string>("OPENSEARCH:URL");
        if (string.IsNullOrEmpty(openSearchUrl))
            throw new InvalidOperationException("OpenSearch URL is not configured.");
        return new Uri(openSearchUrl);
    }

    public string OpenSearchUsername()
    {
        var username = configuration.GetValue<string>("OPENSEARCH:USERNAME");
        return string.IsNullOrEmpty(username)
            ? throw new InvalidOperationException("OpenSearch username is not configured.")
            : username;
    }

    public string OpenSearchPassword()
    {
        var password = configuration.GetValue<string>("OPENSEARCH:PASSWORD");
        return string.IsNullOrEmpty(password)
            ? throw new InvalidOperationException("OpenSearch password is not configured.")
            : password;
    }

    public ConnectionSettings ConfigureTls(ConnectionSettings settings)
    {
        var caCertPath = configuration.GetValue<string>("OPENSEARCH:CA_CERT_PATH");
        if (string.IsNullOrWhiteSpace(caCertPath))
            return settings;
        if (!string.Equals(OpenSearchUrl().Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An OpenSearch CA certificate requires an HTTPS URL.");

        var trustedRoots = new X509Certificate2Collection();
        trustedRoots.ImportFromPemFile(caCertPath);
        if (trustedRoots.Count == 0 || trustedRoots.Cast<X509Certificate2>().Any(certificate =>
                certificate.Extensions.OfType<X509BasicConstraintsExtension>()
                    .All(constraints => !constraints.CertificateAuthority)))
            throw new InvalidOperationException("The OpenSearch CA certificate file must contain CA certificates only.");

        return settings.ServerCertificateValidationCallback((_, certificate, _, errors) =>
            ValidateServerCertificate(certificate, errors, trustedRoots));
    }

    internal static bool ValidateServerCertificate(
        X509Certificate? certificate,
        SslPolicyErrors errors,
        X509Certificate2Collection trustedRoots)
    {
        if (certificate == null ||
            (errors & (SslPolicyErrors.RemoteCertificateNameMismatch |
                       SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
            return false;

        using var serverCertificate =
            X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        using var validationChain = new X509Chain();
        validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        validationChain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
        validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        validationChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        validationChain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        return validationChain.Build(serverCertificate);
    }

    /// <summary>
    /// Base connection settings for an index client. Virtual so tests can plug in an in-memory connection.
    /// </summary>
    public virtual ConnectionSettings CreateConnectionSettings() => new(OpenSearchUrl());

    public static OpenSearchClient NewClient(ConnectionSettings settings) =>
        new(settings);
}

/// <summary>Outcome of <see cref="OpenSearchIndexBase.EnforceRetentionAsync"/>.</summary>
public sealed record IndexRetentionResult(bool RolledOver, long DeletedDocuments, IReadOnlyList<string> DeletedIndices);

public abstract class OpenSearchIndexBase(OpenSearch openSearch, ILogger<OpenSearchIndexBase> logger)
{
    private static readonly ConcurrentDictionary<string, byte> PermissionWarnings = new();
    private static readonly TimeSpan RetentionRequestTimeout = TimeSpan.FromMinutes(10);
    private OpenSearchClient? _client;

    // ReSharper disable once MemberCanBeProtected.Global
    public abstract string IndexName();

    protected abstract string RetentionPolicyId();

    protected abstract string IndexTemplateName();

    protected abstract string BootstrapIndexName();

    protected abstract bool IsRolloverIndex();

    protected abstract Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest> IndexTemplateFunc();

    protected abstract Func<CreateIndexDescriptor, ICreateIndexRequest> IndexFunc();

    protected abstract PostData RetentionPolicy();

    public async Task<OpenSearchClient> Client(CancellationToken stoppingToken = default)
    {
        _client ??= await Initialize(stoppingToken);
        return _client;
    }

    private async Task<OpenSearchClient> Initialize(CancellationToken stoppingToken = default)
    {
        // setup the inital connection settings
        var settings = openSearch.CreateConnectionSettings()
            .DefaultIndex(IndexName())
            .BasicAuthentication(openSearch.OpenSearchUsername(), openSearch.OpenSearchPassword());
        settings = openSearch.ConfigureTls(settings);
        logger.LogInformation(
            $"creating a new opensearch client for index {IndexName()} at {openSearch.OpenSearchUrl()}");


        var client = OpenSearch.NewClient(settings);
        _client = client;

        // send a test ping to ensure we can connect
        logger.LogInformation($"Sending first ping to OpenSearch at {openSearch.OpenSearchUrl()}");
        var pingResp = await client.PingAsync(ct: stoppingToken);
        if (!pingResp.IsValid)
        {
            _client = null;
            throw new InvalidOperationException($"Failed to connect to OpenSearch: {pingResp.DebugInformation}");
        }

        logger.LogInformation("Received valid ping response from OpenSearch");

        // create the retention policy, index template, and bootstrap index if they don't exist
        try
        {
            if (IsRolloverIndex())
            {
                // ISM policy, template and settings are housekeeping: they must never block index reads/writes.
                await RunOptionalSetupStep("create retention policy", CreateRetentionPolicy, stoppingToken);
                await RunOptionalSetupStep("create index template", CreateIndexTemplateIfNotExists, stoppingToken);
                await CreateBootstrapIndexIfNotExistsAsync(stoppingToken);
                await UpdateExistingIndexMappings(client, stoppingToken);
                await RunOptionalSetupStep("update index settings",
                    ct => UpdateExistingIndexSettings(client, ct), stoppingToken);
            }
            else
            {
                await CreateRegularIndexIfNotExists(stoppingToken);
            }
        }
        catch
        {
            _client = null;
            throw;
        }

        return client;
    }

    /// <summary>Runs a setup step that is not required for index usage; any failure is logged and ignored.</summary>
    private async Task RunOptionalSetupStep(
        string operation,
        Func<CancellationToken, Task> step,
        CancellationToken stoppingToken)
    {
        try
        {
            await step(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Optional OpenSearch setup step {Operation} for {Index} failed; continuing without it",
                operation, IndexName());
        }
    }

    protected virtual Task UpdateExistingIndexMappings(
        OpenSearchClient client,
        CancellationToken stoppingToken) =>
        Task.CompletedTask;

    /// <summary>
    /// Refresh interval applied to new and (as a dynamic setting) existing backing indices. Null keeps the server default.
    /// </summary>
    protected virtual Time? RefreshInterval => null;

    /// <summary>Settings and mappings for a bootstrap index created without relying on the index template.</summary>
    protected virtual CreateIndexDescriptor ConfigureNewIndex(CreateIndexDescriptor descriptor) => descriptor;

    /// <summary>Settings and mappings for the index created by <see cref="EnforceRetentionAsync"/>'s rollover.</summary>
    protected virtual RolloverIndexDescriptor ConfigureRollover(RolloverIndexDescriptor descriptor) => descriptor;

    /// <summary>
    /// True when the call failed because the OpenSearch user lacks the privilege (401/403).
    /// Setup calls that need cluster level rights (ISM policies, templates) are optional for index usage.
    /// </summary>
    public static bool IsPermissionDenied(int? httpStatusCode) => httpStatusCode is 401 or 403;

    protected static bool IsPermissionDenied(IResponse response) =>
        IsPermissionDenied(response.ApiCall?.HttpStatusCode);

    /// <summary>Logs a missing-privilege warning once per index type and operation.</summary>
    protected void WarnMissingPermissionOnce(string operation, string? details)
    {
        if (!PermissionWarnings.TryAdd($"{GetType().FullName}|{operation}", 0))
            return;
        logger.LogWarning(
            "OpenSearch user lacks the privilege for {Operation} on {Index}; continuing without it. " +
            "An admin has to apply it out of band (see AneNotifier/INDEX-STORAGE.md). {Details}",
            operation, IndexName(), details);
    }

    private async Task UpdateExistingIndexSettings(OpenSearchClient client, CancellationToken stoppingToken)
    {
        if (RefreshInterval == null)
            return;
        var response = await client.Indices.UpdateSettingsAsync(IndexPattern(), u => u
            .IndexSettings(s => s.RefreshInterval(RefreshInterval)), stoppingToken);
        if (response.IsValid)
            return;
        if (IsPermissionDenied(response))
            WarnMissingPermissionOnce("update index settings", response.ServerError?.Error?.Reason);
        else
            logger.LogWarning("Could not update refresh interval of {Pattern}: {Error}", IndexPattern(), response.DebugInformation);
    }

    /// <summary>
    /// Enforces document retention without relying on ISM (which needs cluster admin rights):
    /// rolls the write index over once it is older than <paramref name="rolloverMaxAge"/> (new index gets the
    /// current settings/mappings), deletes backing indices without documents newer than <paramref name="cutoff"/>
    /// and removes the remaining expired documents with delete-by-query.
    /// Missing privileges for rollover/index deletion are logged once and skipped; delete-by-query failures throw.
    /// </summary>
    /// <param name="dateField">Date field that decides the age (e.g. foundAt)</param>
    public async Task<IndexRetentionResult> EnforceRetentionAsync(
        string dateField,
        DateTime cutoff,
        string? rolloverMaxAge,
        CancellationToken stoppingToken = default)
    {
        var client = await Client(stoppingToken);
        var rolledOver = false;
        var deletedIndices = new List<string>();
        if (IsRolloverIndex())
        {
            if (rolloverMaxAge != null)
                rolledOver = await TryRolloverAsync(client, rolloverMaxAge, stoppingToken);
            deletedIndices.AddRange(await DeleteExpiredBackingIndicesAsync(client, dateField, cutoff, stoppingToken));
        }

        var response = await client.DeleteByQueryAsync<object>(d => d
                .Index(IndexName())
                .Conflicts(Conflicts.Proceed)
                .RequestConfiguration(r => r.RequestTimeout(RetentionRequestTimeout))
                .Query(q => q.DateRange(r => r.Field(dateField).LessThan(cutoff))),
            stoppingToken);
        if (!response.IsValid)
            throw new InvalidOperationException(
                $"Retention delete-by-query on {IndexName()} failed: {response.ServerError?.Error?.Reason ?? response.DebugInformation}");
        return new IndexRetentionResult(rolledOver, response.Deleted, deletedIndices);
    }

    private async Task<bool> TryRolloverAsync(OpenSearchClient client, string maxAge, CancellationToken stoppingToken)
    {
        var response = await client.Indices.RolloverAsync(IndexName(), r => ConfigureRollover(r
            .Conditions(c => c.MaxAge(maxAge))), stoppingToken);
        if (response.IsValid)
        {
            if (response.RolledOver)
                logger.LogInformation("Rolled {Alias} over from {Old} to {New}", IndexName(), response.OldIndex, response.NewIndex);
            return response.RolledOver;
        }
        if (IsPermissionDenied(response))
            WarnMissingPermissionOnce("rollover", response.ServerError?.Error?.Reason);
        else
            logger.LogWarning("Rollover of {Alias} failed: {Error}", IndexName(), response.DebugInformation);
        return false;
    }

    private async Task<IReadOnlyList<string>> DeleteExpiredBackingIndicesAsync(
        OpenSearchClient client, string dateField, DateTime cutoff, CancellationToken stoppingToken)
    {
        var aliases = await client.Indices.GetAliasAsync(IndexPattern(), a => a.Name(IndexName()), stoppingToken);
        if (!aliases.IsValid)
        {
            if (IsPermissionDenied(aliases))
                WarnMissingPermissionOnce("get alias", aliases.ServerError?.Error?.Reason);
            else
                logger.LogWarning("Could not list backing indices of {Alias}: {Error}", IndexName(), aliases.DebugInformation);
            return Array.Empty<string>();
        }

        var deleted = new List<string>();
        foreach (var (index, state) in aliases.Indices)
        {
            var isWriteIndex = state.Aliases.TryGetValue(IndexName(), out var alias) && alias.IsWriteIndex == true;
            if (isWriteIndex || aliases.Indices.Count < 2)
                continue;
            var remaining = await client.CountAsync<object>(c => c
                .Index(index)
                .Query(q => q.DateRange(r => r.Field(dateField).GreaterThanOrEquals(cutoff))), stoppingToken);
            if (!remaining.IsValid || remaining.Count > 0)
                continue;
            var delete = await client.Indices.DeleteAsync(index, ct: stoppingToken);
            if (delete.IsValid)
            {
                logger.LogInformation("Deleted expired backing index {Index} of {Alias}", index.Name, IndexName());
                deleted.Add(index.Name);
            }
            else if (IsPermissionDenied(delete))
                WarnMissingPermissionOnce("delete index", delete.ServerError?.Error?.Reason);
            else
                logger.LogWarning("Could not delete expired index {Index}: {Error}", index.Name, delete.DebugInformation);
        }
        return deleted;
    }
    
    protected string IndexPattern()
    {
        var parts = BootstrapIndexName().Split('-');
        return string.Join('-', parts[..^1]) + "-*";
    }

    private async Task CreateRetentionPolicy(CancellationToken stoppingToken = default)
    {
        var client = await Client(stoppingToken);

        // check if the policy already exists
        var policyExists =
            await client.LowLevel.DoRequestAsync<StringResponse>(GET, $"_plugins/_ism/policies/{RetentionPolicyId()}",
                stoppingToken);

        var policyPath = $"_plugins/_ism/policies/{RetentionPolicyId()}";
        if (IsPermissionDenied(policyExists.HttpStatusCode))
        {
            // The service user usually has no cluster:admin/opendistro/ism rights; retention is then enforced by
            // EnforceRetentionAsync (notifier workers) and the policy can be applied by an admin.
            WarnMissingPermissionOnce("read ISM policy", policyExists.Body);
            return;
        }
        if (policyExists.HttpStatusCode == 200)
        {
            using var existing = JsonDocument.Parse(policyExists.Body);
            var sequenceNumber = existing.RootElement.GetProperty("_seq_no").GetInt64();
            var primaryTerm = existing.RootElement.GetProperty("_primary_term").GetInt64();
            // The path must not contain a query string (OpenSearch.Net throws ArgumentException), pass it as parameters.
            var concurrency = new HttpPutRequestParameters();
            concurrency.SetQueryString("if_seq_no", sequenceNumber);
            concurrency.SetQueryString("if_primary_term", primaryTerm);
            var update = await client.LowLevel.DoRequestAsync<StringResponse>(
                PUT,
                policyPath,
                stoppingToken,
                RetentionPolicy(),
                concurrency);
            if (IsPermissionDenied(update.HttpStatusCode))
            {
                WarnMissingPermissionOnce("update ISM policy", update.Body);
                return;
            }
            if (!update.Success)
                throw new InvalidOperationException(
                    $"Failed to update retention policy: {update.DebugInformation}");

            logger.LogInformation($"Updated retention policy {RetentionPolicyId()}.");
            return;
        }
        if (policyExists.HttpStatusCode != 404)
            throw new InvalidOperationException(
                $"Failed to read retention policy: {policyExists.DebugInformation}");

        logger.LogInformation($"Creating retention policy {RetentionPolicyId()}");
        var res = await client.LowLevel.DoRequestAsync<StringResponse>(PUT,
            policyPath,
            stoppingToken, RetentionPolicy());

        if (IsPermissionDenied(res.HttpStatusCode))
        {
            WarnMissingPermissionOnce("create ISM policy", res.Body);
            return;
        }
        if (!res.Success)
            throw new InvalidOperationException($"Failed to create retention policy: {res.DebugInformation}");
    }


    private async Task CreateIndexTemplateIfNotExists(
        CancellationToken stoppingToken = default
    )
    {
        var client = await Client(stoppingToken);

        var templateResponse =
            await client.Indices.PutTemplateAsync(IndexTemplateName(), IndexTemplateFunc(), stoppingToken);
        if (!templateResponse.IsValid && IsPermissionDenied(templateResponse))
        {
            // New indices still get their settings/mappings: bootstrap and rollover pass them explicitly.
            WarnMissingPermissionOnce("put index template", templateResponse.ServerError?.Error?.Reason);
            return;
        }

        if (!templateResponse.IsValid)
        {
            throw new InvalidOperationException(
                $"Failed to create index template: {templateResponse.DebugInformation}");
        }

        if (templateResponse.Acknowledged)
        {
            logger.LogInformation($"Index template '{IndexTemplateName()}' created successfully.");
            return;
        }

        logger.LogWarning(
            $"Index template '{IndexTemplateName()}' creation was not acknowledged: {templateResponse.DebugInformation}");
    }

    private async Task CreateRegularIndexIfNotExists(CancellationToken stoppingToken)
    {
        var client = await Client(stoppingToken);
        var indexExistsResponse = await client.Indices.ExistsAsync(IndexName(), ct: stoppingToken);
        if (indexExistsResponse.Exists)
        {
            logger.LogInformation($"Index '{IndexName()}' already exists. No action needed.");
            return;
        }

        logger.LogInformation($"Index '{IndexName()}' does not exist. Creating index...");
        var createIndexResponse = await client.Indices.CreateAsync(IndexName(), IndexFunc(), stoppingToken);
        if (!createIndexResponse.IsValid)
        {
            throw new InvalidOperationException(
                $"Failed to create index: {createIndexResponse.DebugInformation}");
        }

        logger.LogInformation($"Successfully created index '{IndexName()}'.");
    }


    private async Task CreateBootstrapIndexIfNotExistsAsync(
        CancellationToken stoppingToken = default)
    {
        var client = await Client(stoppingToken);

        // We check for the ALIAS, not the index. This is the key to idempotency.
        var aliasExistsResponse = await client.Indices.AliasExistsAsync(IndexName(), ct: stoppingToken);

        if (aliasExistsResponse.Exists)
        {
            logger.LogInformation($"Rollover alias '{IndexName()}' already exists. Bootstrap index is not needed.");
            return;
        }

        // If a plain index with the same name as our alias exists, delete it to make way for the rollover alias.
        var indexExistsResponse = await client.Indices.ExistsAsync(IndexName(), ct: stoppingToken);
        if (indexExistsResponse.Exists)
        {
            logger.LogWarning($"Found conflicting plain index '{IndexName()}' where rollover alias should be. Deleting it.");
            var deleteResponse = await client.Indices.DeleteAsync(IndexName(), ct: stoppingToken);
            if (!deleteResponse.IsValid)
                throw new InvalidOperationException($"Failed to delete conflicting index '{IndexName()}': {deleteResponse.DebugInformation}");
        }

        logger.LogInformation(
            $"Rollover alias '{IndexName()}' not found. Creating bootstrap index '{BootstrapIndexName()}'...");

        // The alias doesn't exist, so we create the very first index and assign the alias to it.
        var createIndexResponse = await client.Indices.CreateAsync(BootstrapIndexName(), c => ConfigureNewIndex(c
            .Aliases(a => a
                .Alias(IndexName(), al => al
                    .IsWriteIndex()
                )
            )), stoppingToken);

        if (!createIndexResponse.IsValid &&
            createIndexResponse.ServerError?.Error?.Type != "resource_already_exists_exception")
        {
            throw new InvalidOperationException(
                $"Failed to create bootstrap index: {createIndexResponse.DebugInformation}");
        }

        logger.LogInformation($"Successfully created bootstrap index '{BootstrapIndexName()}' with write alias.");
    }
}


public static class OpenSearchExtension
{
    public static void RegisterOpenSearchServices(this IServiceCollection services)
    {
        services.AddSingleton<OpenSearch>();
        services.AddSingleton<ProductIndex>();
        services.AddSingleton<ListingIndex>();
        services.AddSingleton<ListingSampleIndex>();
        services.AddSingleton<ClothingVisualIndex>();
    }
}
