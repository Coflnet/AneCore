using System.Text.Json;
using Coflnet.Ane.Opensearch;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AneCore.Tests;

public sealed class ListingTitleQueryTests
{
    private static JsonDocument Serialize(string query)
    {
        var client = new OpenSearchClient(new ConnectionSettings(new InMemoryConnection()));
        return JsonDocument.Parse(client.RequestResponseSerializer.SerializeToString(ListingIndex.BuildTitleQuery(query)));
    }

    [TestCase("jacket OR jacke OR veste")]
    [TestCase("jacket|jacke|veste")]
    [TestCase("jacket or jacke OR veste")]
    public void AlternativesSearchAnyAliasInsteadOfRequiringLiteralOr(string query)
    {
        using var body = Serialize(query);
        var clauses = body.RootElement.GetProperty("bool").GetProperty("should").EnumerateArray().ToArray();
        Assert.That(clauses.Select(c => c.GetProperty("match").GetProperty("title").GetProperty("query").GetString()),
            Is.EqualTo(new[] { "jacket", "jacke", "veste" }));
        Assert.That(clauses.All(c => c.GetProperty("match").GetProperty("title").GetProperty("operator").GetString() == "and"), Is.True);
        Assert.That(body.RootElement.GetProperty("bool").GetProperty("minimum_should_match").ToString(), Is.EqualTo("1"));
    }

    [Test]
    public void MultiWordClausesAndSingleQueriesRetainAndSemantics()
    {
        using var single = Serialize("Nike red hoodie");
        var match = single.RootElement.GetProperty("match").GetProperty("title");
        Assert.That(match.GetProperty("query").GetString(), Is.EqualTo("Nike red hoodie"));
        Assert.That(match.GetProperty("operator").GetString(), Is.EqualTo("and"));
        using var alternatives = Serialize("running shoes OR laufschuhe");
        Assert.That(alternatives.RootElement.GetProperty("bool").GetProperty("should")[0].GetProperty("match")
            .GetProperty("title").GetProperty("query").GetString(), Is.EqualTo("running shoes"));
    }

    [Test]
    public void EmptyQueriesDoNotMatchEverythingAndExcessAlternativesAreRejected()
    {
        using var empty = Serialize(" | ");
        Assert.That(empty.RootElement.TryGetProperty("match_none", out _), Is.True);
        Assert.Throws<ArgumentException>(() => ListingIndex.BuildTitleQuery(string.Join(" OR ", Enumerable.Range(0, 17))));
    }

    [Test]
    public void DslLookingTextIsOnlyAMatchValue()
    {
        using var body = Serialize("title:* AND price:[0 TO 1]");
        Assert.That(body.RootElement.EnumerateObject().Single().Name, Is.EqualTo("match"));
        Assert.That(body.RootElement.GetProperty("match").GetProperty("title").GetProperty("query").GetString(),
            Is.EqualTo("title:* AND price:[0 TO 1]"));
    }
}
