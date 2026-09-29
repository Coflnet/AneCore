using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coflnet.Ane.ImageRights;

/// <summary>
/// Reads robots.txt and tdmrep.json of every mapped image host and its marketplace site at start and every
/// 24 hours and stores the verdict. Hosts denied by configuration are written as Revoked without fetching.
/// </summary>
public class ImageRightsRefresher : BackgroundService
{
    public const string UserAgent = "ane.deals crawler/1.0";
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IImageRightsStore store;
    private readonly ImageRightsOptions options;
    private readonly ILogger<ImageRightsRefresher> logger;
    private readonly HttpClient http;

    public ImageRightsRefresher(IImageRightsStore store, ImageRightsOptions options, ILogger<ImageRightsRefresher> logger, HttpClient? http = null)
    {
        this.store = store;
        this.options = options;
        this.logger = logger;
        this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromSeconds(15) };
        if (!this.http.DefaultRequestHeaders.UserAgent.Any())
            this.http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e)
            {
                logger.LogError(e, "Image rights refresh failed");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task RefreshAllAsync(CancellationToken token)
    {
        var cache = new Dictionary<string, FetchedFile>();
        foreach (var entry in ImageHostMapping.KnownHosts)
        {
            try
            {
                await store.UpsertAsync(await BuildRecordAsync(entry, cache, token));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                logger.LogError(e, "Image rights refresh failed for {Host}", entry.ImageHost);
            }
        }
    }

    public async Task<ImageRightsRecord> BuildRecordAsync(ImageHostMapping.Entry entry, Dictionary<string, FetchedFile> cache, CancellationToken token)
    {
        var denial = ImageRightsPolicy.DenialEvidence(entry.ImageHost, options);
        if (denial != null)
            return new ImageRightsRecord
            {
                Host = entry.ImageHost, Platform = entry.Platform.ToString(), Status = nameof(ImageRightsStatus.Revoked),
                Evidence = [denial], CheckedAt = DateTime.UtcNow
            };

        var imageRobots = await FetchCachedAsync(entry.ImageHost, "/robots.txt", cache, token);
        var siteRobots = await FetchCachedAsync(entry.SiteHost, "/robots.txt", cache, token);
        var imageTdm = await FetchCachedAsync(entry.ImageHost, "/.well-known/tdmrep.json", cache, token);
        var siteTdm = await FetchCachedAsync(entry.SiteHost, "/.well-known/tdmrep.json", cache, token);
        var result = ImageRightsEvaluator.Evaluate(new ImageRightsInput(
            entry.ImageHost, entry.SiteHost, imageRobots, siteRobots, imageTdm.Text, siteTdm.Text));
        return new ImageRightsRecord
        {
            Host = entry.ImageHost,
            Platform = entry.Platform.ToString(),
            Status = result.Status.ToString(),
            Evidence = result.Evidence.ToList(),
            CheckedAt = DateTime.UtcNow,
            RobotsHash = Hash(imageRobots.Text, siteRobots.Text)
        };
    }

    public static string Hash(string? imageRobots, string? siteRobots)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((imageRobots ?? "") + "\n\u0000\n" + (siteRobots ?? "")));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<FetchedFile> FetchCachedAsync(string host, string path, Dictionary<string, FetchedFile> cache, CancellationToken token)
    {
        var url = $"https://{host}{path}";
        if (cache.TryGetValue(url, out var cached))
            return cached;
        var file = await FetchAsync(url, token);
        cache[url] = file;
        return file;
    }

    private async Task<FetchedFile> FetchAsync(string url, CancellationToken token)
    {
        try
        {
            using var response = await http.GetAsync(url, token);
            var status = (int)response.StatusCode;
            var body = status is >= 200 and < 300 ? await response.Content.ReadAsStringAsync(token) : null;
            return FetchedFile.FromHttp(status, body);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogWarning("Could not fetch {Url}: {Message}", url, e.Message);
            return FetchedFile.Failed;
        }
    }
}
