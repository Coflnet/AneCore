using System.Text.Json;
using System.Text.RegularExpressions;

namespace Coflnet.Ane.ImageRights;

/// <summary>Recorded image policy, resolved separately for comparison and training. Stored as text in Cassandra.</summary>
public enum ImageRightsStatus
{
    /// <summary>No verdict; a successful policy lookup may default include, an unavailable store is held separately.</summary>
    Unknown,
    /// <summary>No applicable reservation is known for the requested purpose.</summary>
    Allowed,
    /// <summary>A reservation (or a manual denial) exists.</summary>
    Revoked
}

/// <summary>Outcome of one attempt to download a policy file.</summary>
public enum FetchState
{
    /// <summary>2xx, body available (may be empty).</summary>
    Ok,
    /// <summary>The file does not exist (404 or 410): no reservation was published at this path.</summary>
    NotFound,
    /// <summary>Denied access, network error, timeout, 5xx or rate limiting: the answer is unknown.</summary>
    Failed
}

/// <summary>A downloaded policy file (robots.txt / tdmrep.json) or the reason it is missing.</summary>
public sealed record FetchedFile(FetchState State, string? Text)
{
    public static FetchedFile Ok(string? text) => new(FetchState.Ok, text ?? "");
    public static FetchedFile NotFound { get; } = new(FetchState.NotFound, null);
    public static FetchedFile Failed { get; } = new(FetchState.Failed, null);

    /// <summary>Only 404/410 establish absence; denied, rate-limited and failed reads remain unavailable.</summary>
    public static FetchedFile FromHttp(int statusCode, string? body)
    {
        if (statusCode is >= 200 and < 300)
            return Ok(body);
        return statusCode is 404 or 410 ? NotFound : Failed;
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
    IReadOnlyDictionary<string, string>? ImageHeaders = null,
    string UserAgent = ImageRightsEvaluator.UserAgent);

public sealed record ImageRightsResult(ImageRightsStatus Status, IReadOnlyList<string> Evidence);

/// <summary>
/// Collects applicable host/header reservations. ImageRightsPolicy.Resolve selects their purpose;
/// fetch uncertainty never invents a reservation.
/// </summary>
public static class ImageRightsEvaluator
{
    public const string UserAgent = "ane.deals crawler/1.0";
    /// <summary>Legacy names retained for compatibility; their groups do not apply to the ANE crawler.</summary>
    public static readonly string[] AiTrainingCrawlers =
    [
        "GPTBot", "CCBot", "Google-Extended", "ClaudeBot", "anthropic-ai", "Applebot-Extended",
        "Bytespider", "meta-externalagent", "Amazonbot"
    ];

    public static ImageRightsResult Evaluate(ImageRightsInput input)
    {
        var revoked = new List<string>();
        var unknown = new List<string>();

        CheckRobots(input.ImageHost, input.ImageRobots, input.ImagePath, input.UserAgent, revoked, unknown);
        if (!string.Equals(input.SiteHost, input.ImageHost, StringComparison.OrdinalIgnoreCase))
            CheckRobots(input.SiteHost, input.SiteRobots, "/", input.UserAgent, revoked, unknown);

        CheckTdmrep(input.ImageHost, input.ImageTdmrep, input.ImagePath, revoked);
        CheckTdmrep(input.SiteHost, input.SiteTdmrep, "/", revoked);
        CheckHeaders(input.ImageHeaders, input.UserAgent, revoked);

        if (revoked.Count > 0)
            return new ImageRightsResult(ImageRightsStatus.Revoked, revoked);
        if (unknown.Count > 0)
            return new ImageRightsResult(ImageRightsStatus.Unknown, unknown);
        return new ImageRightsResult(ImageRightsStatus.Allowed,
            [$"no reservation found in robots.txt of {input.ImageHost} and {input.SiteHost}"]);
    }

    private static void CheckRobots(string host, FetchedFile file, string path, string userAgent, List<string> revoked, List<string> unknown)
    {
        if (file.State == FetchState.Failed)
        {
            unknown.Add($"robots.txt of {host} could not be fetched");
            return;
        }
        if (file.State == FetchState.NotFound || string.IsNullOrWhiteSpace(file.Text))
            return;

        var robots = RobotsTxt.Parse(file.Text);
        var groups = robots.Groups.Select(group => (Group: group, Specificity: group.Agents
            .Where(a => a != "*" && userAgent.Contains(a, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Length).DefaultIfEmpty(0).Max())).ToList();
        var specificity = groups.Select(g => g.Specificity).DefaultIfEmpty(0).Max();
        var applicable = groups.Where(g => specificity > 0 ? g.Specificity == specificity : g.Group.Agents.Contains("*")).Select(g => g.Group).ToList();
        foreach (var line in robots.DirectiveLines.Concat(applicable.SelectMany(g => g.Directives)))
        {
            if (line.Name.Equals("content-signal", StringComparison.OrdinalIgnoreCase)
                && SplitTokens(line.Value).Any(t => t.Equals("ai-train=no", StringComparison.OrdinalIgnoreCase)))
                revoked.Add($"robots.txt of {host}: Content-Signal ai-train=no - \"{line.Raw}\"");
            if (line.Name.Equals("content-signal", StringComparison.OrdinalIgnoreCase))
                foreach (var token in new[] { "ai-input=no", "use=immediate" })
                    if (SplitTokens(line.Value).Any(t => t.Equals(token, StringComparison.OrdinalIgnoreCase)))
                        revoked.Add($"robots.txt of {host}: Content-Signal {token} - \"{line.Raw}\"");
            if (line.Name.Equals("content-usage", StringComparison.OrdinalIgnoreCase)
                && SplitTokens(line.Value).Any(t => t.Equals("train-ai=n", StringComparison.OrdinalIgnoreCase)))
                revoked.Add($"robots.txt of {host}: Content-Usage train-ai=n - \"{line.Raw}\"");
        }

        var rules = applicable.SelectMany(g => g.Rules).Where(r => r.Value.Length > 0 && LocationMatches(r.Value, path)).ToList();
        var longest = rules.Select(r => r.Value.Replace("*", "").TrimEnd('$').Length).DefaultIfEmpty(-1).Max();
        var strongest = rules.Where(r => r.Value.Replace("*", "").TrimEnd('$').Length == longest).ToList();
        if (strongest.Any(r => r.Name == "disallow") && !strongest.Any(r => r.Name == "allow"))
            revoked.Add($"robots.txt of {host}: crawler {userAgent} is disallowed for {path}");
    }

    private static IEnumerable<string> SplitTokens(string value) =>
        Regex.Replace(value, @"\s*=\s*", "=").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    /// <summary>Applicable response-header restrictions; newline-joined physical fields reset crawler scope.</summary>
    public static bool HeaderReserves(string name, string value, ImageRightsPurpose purpose, string userAgent = UserAgent)
    {
        if (name.Equals("tdm-reservation", StringComparison.OrdinalIgnoreCase)) return Regex.Split(value.Trim(), @"[,\s]+").Any(part => part == "1");
        if (name.Equals("content-signal", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(value, purpose == ImageRightsPurpose.Comparison
                ? @"(?:^|[,\s;])(?:ai-input\s*=\s*no|use\s*=\s*immediate)(?=$|[,\s;])"
                : @"(?:^|[,\s;])(?:ai-train\s*=\s*no|use\s*=\s*immediate)(?=$|[,\s;])", RegexOptions.IgnoreCase);
        if (!name.Equals("x-robots-tag", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var field in value.Split('\n'))
        {
            var agent = "*";
            foreach (var token in field.Split(','))
            {
                var directive = token.Trim();
                var colon = directive.IndexOf(':');
                if (colon >= 0)
                {
                    agent = directive[..colon].Trim();
                    directive = directive[(colon + 1)..].Trim();
                }
                var applicable = agent == "*" || agent.Equals("ane.deals", StringComparison.OrdinalIgnoreCase)
                    || agent.Equals(userAgent, StringComparison.OrdinalIgnoreCase)
                    || !Regex.IsMatch(agent, @"^[a-z0-9_.*/-]+$", RegexOptions.IgnoreCase);
                if (applicable && directive.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Any(t => t.Equals("noai", StringComparison.OrdinalIgnoreCase) || t.Equals("noimageai", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        return false;
    }

    private static void CheckHeaders(IReadOnlyDictionary<string, string>? headers, string userAgent, List<string> revoked)
    {
        if (headers == null) return;
        foreach (var (name, value) in headers)
        {
            if (!HeaderReserves(name, value, ImageRightsPurpose.Training, userAgent) && !HeaderReserves(name, value, ImageRightsPurpose.Comparison, userAgent)) continue;
            var canonical = name.Equals("tdm-reservation", StringComparison.OrdinalIgnoreCase) ? "tdm-reservation"
                : name.Equals("x-robots-tag", StringComparison.OrdinalIgnoreCase) ? "X-Robots-Tag" : "Content-Signal";
            revoked.Add($"image response header {canonical}: {value}");
        }
    }

}

/// <summary>Minimal robots.txt reader: groups of user agents with rules plus every other directive line.</summary>
public sealed class RobotsTxt
{
    public sealed record Line(string Name, string Value, string Raw);
    public sealed record Group(List<string> Agents, List<Line> Rules)
    {
        public List<Line> Directives { get; } = [];
    }

    public List<Group> Groups { get; } = [];
    /// <summary>Ungrouped directives; user-agent-specific directives remain on their group.</summary>
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
                    if (current == null || current.Rules.Count > 0 || current.Directives.Count > 0)
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
                    if (current == null) result.DirectiveLines.Add(parsed);
                    else current.Directives.Add(parsed);
                    break;
            }
        }
        return result;
    }
}
