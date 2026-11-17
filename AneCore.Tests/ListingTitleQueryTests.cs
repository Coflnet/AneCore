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

    [TestCase("xiaomi 14 t pro")]
    [TestCase("xiaomi 14t pro")]
    [TestCase("xiaomi 14\u00A0t pro")]
    public void ModelSuffixAcceptsJoinedTokenOrBothPartsWithoutPositionQueries(string query)
    {
        using var body = Serialize(query);
        Assert.That(body.RootElement.GetRawText(), Does.Not.Contain("match_phrase"), "title has no positions indexed");
        var must = body.RootElement.GetProperty("bool").GetProperty("must").EnumerateArray().ToArray();
        var rest = must[0].GetProperty("match").GetProperty("title");
        Assert.That(rest.GetProperty("query").GetString(), Is.EqualTo("xiaomi pro"));
        Assert.That(rest.GetProperty("operator").GetString(), Is.EqualTo("and"));
        var model = must[1].GetProperty("bool");
        var should = model.GetProperty("should").EnumerateArray().ToArray();
        Assert.That(should[0].GetProperty("match").GetProperty("title").GetProperty("query").GetString(), Is.EqualTo("14t"));
        var parts = should[1].GetProperty("bool").GetProperty("must").EnumerateArray()
            .Select(c => c.GetProperty("match").GetProperty("title").GetProperty("query").GetString());
        Assert.That(parts, Is.EqualTo(new[] { "14", "t" }));
        Assert.That(model.GetProperty("minimum_should_match").ToString(), Is.EqualTo("1"));
    }

    [Test]
    public void ModelSuffixKeepsDecimalNumbersInTheTextClause()
    {
        using var body = Serialize("kabel 1.5 4k");
        var rest = body.RootElement.GetProperty("bool").GetProperty("must")[0].GetProperty("match").GetProperty("title");
        Assert.That(rest.GetProperty("query").GetString(), Is.EqualTo("kabel 1.5"));
    }

    [Test]
    public void SymbolOnlyWordsDoNotProduceAnEmptyTextClause()
    {
        using var body = Serialize("5g / 4g");
        var must = body.RootElement.GetProperty("bool").GetProperty("must").EnumerateArray().ToArray();
        Assert.That(must.Length, Is.EqualTo(2));
        Assert.That(must.All(c => c.TryGetProperty("bool", out _)), Is.True, "only the two model clauses, no match for '/'");
    }

    [Test]
    public void ModelSuffixWorksInsideAlternatives()
    {
        using var body = Serialize("xiaomi 14 t OR redmi 13");
        var should = body.RootElement.GetProperty("bool").GetProperty("should").EnumerateArray().ToArray();
        Assert.That(should[0].TryGetProperty("bool", out _), Is.True);
        Assert.That(should[1].GetProperty("match").GetProperty("title").GetProperty("query").GetString(), Is.EqualTo("redmi 13"));
    }
}
