using Cassandra;
using Coflnet.Ane.KnownProducts;
using System.Text.Json;

namespace AneCore.Tests;

public class KnownProductRetirementTests
{
    private static KnownProduct Clone(KnownProduct p) => JsonSerializer.Deserialize<KnownProduct>(JsonSerializer.Serialize(p))!;

    [Test]
    public void ReviewedRowsAreNotActiveSeeds_AndTheirAliasesReachTheCanonicalWithGuards()
    {
        var active = KnownProductSeed.LoadAll(true).ToDictionary(p => p.Id);
        var retired = KnownProductSeed.LoadRetirements();
        Assert.That(retired.Select(p => p.Expected.Id), Is.EquivalentTo(new[] { "benq-benqgw2255", "lg-lg-d2343pb-bn", "rca-rcahp5043", "rca-rcahp59micwh" }));
        foreach (var (expected, canonical) in retired)
        {
            Assert.That(active.ContainsKey(expected.Id), Is.False);
            Assert.That(expected.Aliases.All(a => active[canonical.Id].Aliases.Contains(a)), Is.True);
        }
        Assert.That(active["benq-gw2255"].ExcludeTerms, Does.Contain("hm"));
        var matcher = new KnownProductMatcher(active.Values.ToList());
        Assert.That(matcher.Match("BenQ BENQGW2255 HM")?.Id, Is.Not.EqualTo("benq-gw2255"));
        Assert.That(matcher.Match("BenQ GW2255 HM")?.Id, Is.Not.EqualTo("benq-gw2255"));
        Assert.That(active.ContainsKey("rca-rcahp5042") && active.ContainsKey("rca-hp5042"), Is.True);
        Assert.That(active.ContainsKey("rca-rcahp60a") && active.ContainsKey("rca-hp60a"), Is.True);
        Assert.That(active["rca-hp5043"].PossibleAttributes, Is.Empty);
    }

    [TestCase("brand")]
    [TestCase("model")]
    [TestCase("name")]
    [TestCase("vertical")]
    [TestCase("category")]
    [TestCase("alias")]
    [TestCase("attribute")]
    [TestCase("exclusion")]
    [TestCase("admin")]
    [TestCase("timestamp")]
    [TestCase("unchanged")]
    public async Task ConditionalDeletePreservesEveryChangedFieldAndIsIdempotent(string change)
    {
        var expected = KnownProductSeed.LoadRetirements()[0].Expected;
        var actual = Clone(expected);
        switch (change)
        {
            case "brand": actual.Brand += "!"; break;
            case "model": actual.Model += "!"; break;
            case "name": actual.Name += "!"; break;
            case "vertical": actual.Vertical += "!"; break;
            case "category": actual.Categories.Reverse(); break;
            case "alias": actual.Aliases.Add("custom"); break;
            case "attribute": actual.PossibleAttributes["custom"] = ["value"]; break;
            case "exclusion": actual.ExcludeTerms.Add("custom"); break;
            case "admin": actual.Source = "admin"; break;
            case "timestamp": actual.VerifiedAt = actual.VerifiedAt.AddMilliseconds(1); break;
        }
        var store = new InMemoryKnownProductStore([actual]);
        Assert.That(await store.DeleteSeedIfUnchangedAsync(expected), Is.EqualTo(change == "unchanged"));
        Assert.That(await store.GetAsync(expected.Id) == null, Is.EqualTo(change == "unchanged"));
        await store.DeleteAsync(expected.Id);
        Assert.That(await store.DeleteSeedIfUnchangedAsync(expected), Is.False);
    }

    [Test]
    public async Task EvenAnExactAdminPreimageCannotBeDeleted()
    {
        var admin = KnownProductSeed.LoadRetirements()[0].Expected;
        admin.Source = "admin";
        var store = new InMemoryKnownProductStore([admin]);
        Assert.That(await store.DeleteSeedIfUnchangedAsync(Clone(admin)), Is.False);
        Assert.That(await store.GetAsync(admin.Id), Is.SameAs(admin));
    }

    [Test]
    public void CassandraStatementComparesAllFields_AndBindsNullForEmptyMultiCellCollections()
    {
        var p = KnownProductSeed.LoadRetirements()[0].Expected;
        var statement = CassandraKnownProductStore.BuildSeedDeleteStatement(p);
        Assert.That(statement.QueryString, Is.EqualTo("DELETE FROM known_products WHERE id = ? IF source = ? AND verified_at = ? AND brand = ? AND model = ? AND name = ? AND vertical = ? AND categories = ? AND aliases = ? AND possible_attributes = ? AND exclude_terms = ?"));
        Assert.That(statement.QueryValues, Is.EqualTo(new object?[] { p.Id, p.Source, p.VerifiedAt, p.Brand, p.Model, p.Name, p.Vertical, p.Categories, p.Aliases, p.PossibleAttributes, null }));
        Assert.That(statement.QueryValues, Has.Length.EqualTo(11));
        p.Categories.Clear(); p.Aliases.Clear(); p.PossibleAttributes.Clear();
        Assert.That(CassandraKnownProductStore.BuildSeedDeleteStatement(p).QueryValues.Skip(7), Is.All.Null);
        p.PossibleAttributes["field"] = []; // Frozen nested empty sets remain values; only the empty outer map is null.
        p.ExcludeTerms.Add("guard");
        var populated = CassandraKnownProductStore.BuildSeedDeleteStatement(p).QueryValues;
        Assert.That(populated[9], Is.SameAs(p.PossibleAttributes));
        Assert.That(populated[10], Is.SameAs(p.ExcludeTerms));
    }
}
