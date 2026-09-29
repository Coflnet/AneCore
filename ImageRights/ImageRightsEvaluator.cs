using System.Text.Json;

namespace Coflnet.Ane.ImageRights;

/// <summary>Whether photos of a host may be used for training. Stored as text (the name) in Cassandra.</summary>
public enum ImageRightsStatus
{
    /// <summary>Nothing known (or the knowledge is stale): treated like <see cref="Revoked"/> by every consumer.</summary>
    Unknown,
    /// <summary>Both hosts were read and no reservation was found.</summary>
    Allowed,
    /// <summary>A reservation (or a manual denial) exists.</summary>
    Revoked
}

/// <summary>Outcome of one attempt to download a policy file.</summary>
public enum FetchState
{
    /// <summary>2xx, body available (may be empty).</summary>
    Ok,
    /// <summary>The file does not exist or is not accessible (404, 403, other 4xx): counts as "no signal".</summary>
    NotFound,
    /// <summary>Network error, timeout, 5xx or rate limiting: the answer is unknown.</summary>
    Failed
}

/// <summary>A downloaded policy file (robots.txt / tdmrep.json) or the reason it is missing.</summary>
public sealed record FetchedFile(FetchState State, string? Text)
{
    public static FetchedFile Ok(string? text) => new(FetchState.Ok, text ?? "");
    public static FetchedFile NotFound { get; } = new(FetchState.NotFound, null);
    public static FetchedFile Failed { get; } = new(FetchState.Failed, null);

    /// <summary>Maps an HTTP status to a result. 5xx, 429 and 408 are failures, every other non-2xx is "no file".</summary>
    public static FetchedFile FromHttp(int statusCode, string? body)
    {
        if (statusCode is >= 200 and < 300)
            return Ok(body);
        if (statusCode >= 500 || statusCode == 429 || statusCode == 408)
            return Failed;
        return NotFound;
    }
}

/// <summary>Everything the evaluator needs about one image host and the marketplace site it belongs to.</summary>
public sealed record ImageRightsInput(
    string ImageHost,
    string SiteHost,
    FetchedFile ImageRobots,
    FetchedFile SiteRobots,
    string? ImageTdmrep = null,
    string? SiteTdmrep = null,
    string ImagePath = "/",
    IReadOnlyDictionary<string, string>? ImageHeaders = null);

public sealed record ImageRightsResult(ImageRightsStatus Status, IReadOnlyList<string> Evidence);

/// <summary>
/// Pure, conservative decision whether photos of a host may be used to train a model. A reservation
/// anywhere wins; only when both hosts were read completely and nothing fired the result is Allowed.
/// </summary>
public static class ImageRightsEvaluator
{
    /// <summary>Crawler names of AI training bots. A group naming one of them with <c>Disallow: /</c> is a reservation.</summary>
    public static readonly string[] AiTrainingCrawlers =
    [
        "GPTBot", "CCBot", "Google-Extended", "ClaudeBot", "anthropic-ai", "Applebot-Extended",
        "Bytespider", "meta-externalagent", "Amazonbot"
    ];

    public static ImageRightsResult Evaluate(ImageRightsInput input)
    {
        var revoked = new List<string>();
        var unknown = new List<string>();

        CheckRobots(input.ImageHost, input.ImageRobots, revoked, unknown);
        if (!string.Equals(input.SiteHost, input.ImageHost, StringComparison.OrdinalIgnoreCase))
            CheckRobots(input.SiteHost, input.SiteRobots, revoked, unknown);

        CheckTdmrep(input.ImageHost, input.ImageTdmrep, input.ImagePath, revoked);
        CheckTdmrep(input.SiteHost, input.SiteTdmrep, "/", revoked);
        CheckHeaders(input.ImageHeaders, revoked);

        if (revoked.Count > 0)
            return new ImageRightsResult(ImageRightsStatus.Revoked, revoked);
        if (unknown.Count > 0)
            return new ImageRightsResult(ImageRightsStatus.Unknown, unknown);
        return new ImageRightsResult(ImageRightsStatus.Allowed,
            [$"no reservation found in robots.txt of {input.ImageHost} and {input.SiteHost}"]);
    }

    private static void CheckRobots(string host, FetchedFile file, List<string> revoked, List<string> unknown)
    {
        if (file.State == FetchState.Failed)
        {
            unknown.Add($"robots.txt of {host} could not be fetched");
            return;
        }
        if (file.State == FetchState.NotFound || string.IsNullOrWhiteSpace(file.Text))
            return;

        var robots = RobotsTxt.Parse(file.Text);
        foreach (var line in robots.DirectiveLines)
        {
            if (line.Name.Equals("content-signal", StringComparison.OrdinalIgnoreCase)
                && SplitTokens(line.Value).Any(t => t.Equals("ai-train=no", StringComparison.OrdinalIgnoreCase)))
                revoked.Add($"robots.txt of {host}: Content-Signal ai-train=no - \"{line.Raw}\"");
            else if (line.Name.Equals("content-usage", StringComparison.OrdinalIgnoreCase)
                && SplitTokens(line.Value).Any(t => t.Equals("train-ai=n", StringComparison.OrdinalIgnoreCase)))
                revoked.Add($"robots.txt of {host}: Content-Usage train-ai=n - \"{line.Raw}\"");
        }

        foreach (var group in robots.Groups)
        {
            var named = group.Agents.FirstOrDefault(a =>
                AiTrainingCrawlers.Any(c => c.Equals(a, StringComparison.OrdinalIgnoreCase)));
            if (named == null)
                continue;
            var disallowRoot = group.Rules.FirstOrDefault(r => r.Name == "disallow" && IsRoot(r.Value));
            if (disallowRoot == null)
                continue;
            // An explicit root Allow ties with the root Disallow and the more permissive rule wins (RFC 9309).
            if (group.Rules.Any(r => r.Name == "allow" && IsRoot(r.Value)))
                continue;
            revoked.Add($"robots.txt of {host}: AI crawler group ({string.Join(", ", group.Agents)}) has Disallow: / - \"{disallowRoot.Raw}\"");
        }
    }

    private static bool IsRoot(string value) => value is "/" or "/*";

    private static IEnumerable<string> SplitTokens(string value) =>
        value.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Replace(" ", ""));

    private static void CheckTdmrep(string host, string? json, string path, List<string> revoked)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return; }
        using (doc)
        {
            var entries = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement.EnumerateArray().ToList(),
                JsonValueKind.Object => [doc.RootElement],
                _ => []
            };
            foreach (var entry in entries)
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                if (!TryGetProperty(entry, "tdm-reservation", out var reservation) || !IsOne(reservation))
                    continue;
                var location = TryGetProperty(entry, "location", out var loc) && loc.ValueKind == JsonValueKind.String
                    ? loc.GetString() ?? "/" : "/";
                if (LocationMatches(location, path) || location == "/")
                    revoked.Add($"tdmrep.json of {host}: tdm-reservation 1 for location \"{location}\" - {entry.GetRawText()}");
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var p in element.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool IsOne(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number ? e.TryGetInt32(out var n) && n == 1
        : e.ValueKind == JsonValueKind.String && e.GetString()?.Trim() == "1";

    /// <summary>Robots-style location pattern: a prefix of the path, <c>*</c> matches any characters, <c>$</c> anchors the end.</summary>
    public static bool LocationMatches(string pattern, string path)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "/")
            return true;
        var anchored = pattern.EndsWith('$');
        if (anchored)
            pattern = pattern[..^1];
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + (anchored ? "$" : "");
        return System.Text.RegularExpressions.Regex.IsMatch(path, regex);
    }

    private static void CheckHeaders(IReadOnlyDictionary<string, string>? headers, List<string> revoked)
    {
        if (headers == null)
            return;
        foreach (var (name, value) in headers)
        {
            if (name.Equals("tdm-reservation", StringComparison.OrdinalIgnoreCase) && value.Trim() == "1")
                revoked.Add($"image response header tdm-reservation: {value}");
            else if (name.Equals("x-robots-tag", StringComparison.OrdinalIgnoreCase)
                && (value.Contains("noai", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("noimageai", StringComparison.OrdinalIgnoreCase)))
                revoked.Add($"image response header X-Robots-Tag: {value}");
        }
    }
}

/// <summary>Minimal robots.txt reader: groups of user agents with rules plus every other directive line.</summary>
public sealed class RobotsTxt
{
    public sealed record Line(string Name, string Value, string Raw);
    public sealed record Group(List<string> Agents, List<Line> Rules);

    public List<Group> Groups { get; } = [];
    /// <summary>Content-Signal, Content-Usage and other non-group directives anywhere in the file.</summary>
    public List<Line> DirectiveLines { get; } = [];

    public static RobotsTxt Parse(string text)
    {
        var result = new RobotsTxt();
        Group? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim('\r', ' ', '\t');
            var hash = line.IndexOf('#');
            var content = (hash >= 0 ? line[..hash] : line).Trim();
            var colon = content.IndexOf(':');
            if (content.Length == 0 || colon <= 0)
                continue;
            var name = content[..colon].Trim().ToLowerInvariant();
            var value = content[(colon + 1)..].Trim();
            var parsed = new Line(name, value, content);
            switch (name)
            {
                case "user-agent":
                    // consecutive user-agent lines share one group; a user-agent after a rule starts a new one
                    if (current == null || current.Rules.Count > 0)
                    {
                        current = new Group([], []);
                        result.Groups.Add(current);
                    }
                    current.Agents.Add(value);
                    break;
                case "allow":
                case "disallow":
                    current?.Rules.Add(parsed);
                    break;
                default:
                    result.DirectiveLines.Add(parsed);
                    break;
            }
        }
        return result;
    }
}
