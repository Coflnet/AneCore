using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;
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

    public static OpenSearchClient NewClient(ConnectionSettings settings) =>
        new(settings);
}

public abstract class OpenSearchIndexBase(OpenSearch openSearch, ILogger<OpenSearchIndexBase> logger)
{
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
        var settings = new ConnectionSettings(openSearch.OpenSearchUrl())
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
                await CreateRetentionPolicy(stoppingToken);
                await CreateIndexTemplateIfNotExists(stoppingToken);
                await CreateBootstrapIndexIfNotExistsAsync(stoppingToken);
                await UpdateExistingIndexMappings(client, stoppingToken);
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

    protected virtual Task UpdateExistingIndexMappings(
        OpenSearchClient client,
        CancellationToken stoppingToken) =>
        Task.CompletedTask;
    
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
        if (policyExists.HttpStatusCode == 200)
        {
            using var existing = JsonDocument.Parse(policyExists.Body);
            var sequenceNumber = existing.RootElement.GetProperty("_seq_no").GetInt64();
            var primaryTerm = existing.RootElement.GetProperty("_primary_term").GetInt64();
            var update = await client.LowLevel.DoRequestAsync<StringResponse>(
                PUT,
                $"{policyPath}?if_seq_no={sequenceNumber}&if_primary_term={primaryTerm}",
                stoppingToken,
                RetentionPolicy());
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
        var createIndexResponse = await client.Indices.CreateAsync(BootstrapIndexName(), c => c
            .Aliases(a => a
                .Alias(IndexName(), al => al
                    .IsWriteIndex()
                )
            ), stoppingToken);

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
    }
}
