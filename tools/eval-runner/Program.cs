using System.Diagnostics;
using System.Text.Json;
using Coflnet.Ane.KnownProducts;

// Throwaway console app for the catalog-import task's precision/performance evaluation - reads
// eval/titles.json (real public listing titles pulled from the Ane API), runs the full expanded-catalog
// matcher over every title, and writes eval/results.json plus a few performance numbers to stdout.

var evalDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval");
var titlesPath = Path.GetFullPath(Path.Combine(evalDir, "titles.json"));
var resultsPath = Path.GetFullPath(Path.Combine(evalDir, "results.json"));

Console.WriteLine($"Reading titles from {titlesPath}");
var titlesJson = File.ReadAllText(titlesPath);
var titles = JsonSerializer.Deserialize<List<TitleEntry>>(titlesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("no titles");

var seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
Console.WriteLine($"Loaded {seed.Count} known products across the full catalogue");

var buildSw = Stopwatch.StartNew();
var matcher = new KnownProductMatcher(seed);
buildSw.Stop();
Console.WriteLine($"Matcher index build time: {buildSw.Elapsed.TotalMilliseconds:F1}ms");

var results = new List<ResultEntry>();
var matchSw = Stopwatch.StartNew();
foreach (var t in titles)
{
    var match = matcher.Match(t.Title);
    results.Add(new ResultEntry(t.Term, t.SeoId, t.Title, match?.Id, match?.Name));
}
matchSw.Stop();

var matched = results.Count(r => r.MatchedId != null);
Console.WriteLine($"Matched {matched}/{results.Count} titles ({(double)matched / results.Count:P1})");
Console.WriteLine($"Total match time for {results.Count} titles: {matchSw.Elapsed.TotalMilliseconds:F1}ms " +
                   $"({matchSw.Elapsed.TotalMilliseconds / results.Count:F4}ms/title)");

// 10k-call perf number against the full catalogue (task: "10k Match calls ... < 2s"), reusing a real title.
var perfTitle = titles.Count > 0 ? titles[0].Title : "Apple iPhone 14 Pro Max 256GB";
var perfSw = Stopwatch.StartNew();
for (var i = 0; i < 10_000; i++)
    matcher.Match(perfTitle);
perfSw.Stop();
Console.WriteLine($"10,000 Match calls on full catalogue ({seed.Count} products): {perfSw.Elapsed.TotalMilliseconds:F1}ms");

// Rough managed-memory footprint of the snapshot (GC.GetAllocatedBytesForCurrentThread delta is noisy
// with JIT warmup already done above; report process working set instead as the practically relevant number).
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var workingSetMb = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
Console.WriteLine($"Process working set after building matcher + running eval: {workingSetMb:F1}MB");

var byTerm = results.GroupBy(r => r.Term).Select(g => new
{
    term = g.Key,
    total = g.Count(),
    matched = g.Count(r => r.MatchedId != null),
}).OrderBy(x => x.term).ToList();

File.WriteAllText(resultsPath, JsonSerializer.Serialize(new
{
    totalTitles = results.Count,
    matchedCount = matched,
    matchRate = (double)matched / results.Count,
    matcherIndexBuildMs = buildSw.Elapsed.TotalMilliseconds,
    totalMatchMs = matchSw.Elapsed.TotalMilliseconds,
    msPerTitle = matchSw.Elapsed.TotalMilliseconds / results.Count,
    tenThousandCallsMs = perfSw.Elapsed.TotalMilliseconds,
    processWorkingSetMb = workingSetMb,
    catalogSize = seed.Count,
    byTerm,
    results,
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {resultsPath}");

internal record TitleEntry(string Term, string SeoId, string Title);
internal record ResultEntry(string Term, string SeoId, string Title, string? MatchedId, string? MatchedName);
