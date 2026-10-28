using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexCableToolCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-10-28-r39-knipex-cable";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    // Exact article, Length cell and named tool family from the official manufacturer variant table.
    public static readonly object[][] ManufacturerRows =
    {
        new object[] { "95 11 160", "160", "cable_shears" },
        new object[] { "95 12 160", "160", "cable_shears" },
        new object[] { "95 16 160", "160", "cable_shears" },
        new object[] { "95 11 225", "225", "cable_shears" },
        new object[] { "95 12 225", "225", "cable_shears" },
        new object[] { "95 18 225", "225", "cable_shears" },
        new object[] { "95 11 165", "165", "cable_shears" },
        new object[] { "95 12 165", "165", "cable_shears" },
        new object[] { "95 16 165", "165", "cable_shears" },
        new object[] { "95 41 165", "165", "cable_shears" },
        new object[] { "95 21 165", "165", "cable_shears" },
        new object[] { "95 22 165", "165", "cable_shears" },
        new object[] { "95 26 165", "165", "cable_shears" },
        new object[] { "95 11 200", "200", "cable_shears" },
        new object[] { "95 12 200", "200", "cable_shears" },
        new object[] { "95 16 200", "200", "cable_shears" },
        new object[] { "95 17 200", "200", "cable_shears" },
        new object[] { "95 31 250", "250", "ratchet_cable_cutter" },
        new object[] { "95 31 280", "280", "ratchet_cable_cutter" },
        new object[] { "95 36 250", "250", "ratchet_cable_cutter" },
        new object[] { "95 36 280", "280", "ratchet_cable_cutter" },
        new object[] { "95 32 320", "320", "ratchet_cable_cutter" },
        new object[] { "95 36 320", "320", "ratchet_cable_cutter" },
        new object[] { "95 32 060", "630", "ratchet_cable_cutter" },
        new object[] { "95 32 100", "680", "ratchet_cable_cutter" },
        new object[] { "95 32 315 A", "315", "ratchet_cable_cutter" },
        new object[] { "95 36 315 A", "315", "ratchet_cable_cutter" },
        new object[] { "95 61 190", "190", "wire_rope_cutter" },
        new object[] { "95 62 190", "190", "wire_rope_cutter" },
        new object[] { "95 62 160", "160", "wire_rope_cutter" },
        new object[] { "95 05 165", "165", "cable_shears" },
        new object[] { "95 06 230", "230", "cable_shears" },
        new object[] { "95 61 150", "150", "bowden_cable_cutter" },
        new object[] { "95 12 500", "500", "cable_shears" },
        new object[] { "95 17 500", "500", "cable_shears" },
        new object[] { "95 27 600", "600", "cable_shears" },
        new object[] { "94 55 200", "200", "anvil_shears" },
        new object[] { "95 32 038", "570", "ratchet_cable_shears" },
        new object[] { "95 32 340 SR", "340", "ratchet_cable_cutter" },
    };

    [TestCaseSource(nameof(ManufacturerRows))]
    public void ExactManufacturerBodyHasOneIdentityAndItsExplicitLength(string article, string length, string type)
    {
        var id = "knipex-" + article.ToLowerInvariant().Replace(" ", "");
        var row = Catalog.Single(p => p.Source == Source && p.Model == article);
        Assert.That(row.Id, Is.EqualTo(id));
        Assert.That(Catalog.Count(p => p.Id == id || p.Brand == "KNIPEX" && p.Model == article), Is.EqualTo(1));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "tool_type", "article_number", "length_mm" }));
        foreach (var (key, value) in new[] { ("product_kind", "hand_tool"), ("tool_type", type), ("article_number", article), ("length_mm", length) })
            Assert.That(row.PossibleAttributes[key], Is.EquivalentTo(new[] { value }), article);
        foreach (var title in row.Aliases.Append("KNIPEX outil " + article))
        {
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
            Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
            Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True, title);
        }
    }

    [Test]
    public void CohortContainsOnlyTheThirtyNineVerifiedBodiesWithoutBaselineAliasCollisions()
    {
        var added = Catalog.Where(p => p.Source == Source).ToList();
        Assert.That(added, Has.Count.EqualTo(39));
        Assert.That(added.Select(p => p.Model), Is.EquivalentTo(ManufacturerRows.Select(r => (string)r[0])));
        var existingAliases = Catalog.Where(p => p.Source != Source).SelectMany(p => p.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(added.SelectMany(p => p.Aliases).Select(KnownProductMatcher.Normalize).Where(existingAliases.Contains), Is.Empty);
    }

    [Test]
    public void ObservedSwedishCableCutterNowReusesTheVerifiedArticle()
    {
        const string title = "Knipex kabelsax 95 32 320 papegoja"; // Blocket27661538; no condition/contents inferred.
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo("knipex-9532320"));
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo("knipex-9532320"));
        Assert.That(ResaleCatalog.Match(title)!.PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { "320" }));
    }

    [TestCase("KNIPEX 95 32 32")]
    [TestCase("KNIPEX 95 32 3200")]
    [TestCase("KNIPEX 95 32 320FOO")]
    [TestCase("KNIPEX 95 32 320/01")]
    [TestCase("KNIPEX 95 32 320-FOO")]
    [TestCase("KNIPEX 95 32 320_FOO")]
    [TestCase("KNIPEX 95 32 320.01")]
    [TestCase("KNIPEX 95 32 320,01")]
    [TestCase("KNIPEX 95 32 320/ 01")]
    [TestCase("Knipex kabelsax 95 32 320 Ersatzkabel")]
    [TestCase("DJI kabelsax Mini 4 Pro")]
    [TestCase("KNIPEX 95 32 320 T")]
    [TestCase("KNIPEX 95 62 160 TC")]
    [TestCase("KNIPEX 95 11 165 SB")]
    [TestCase("KNIPEX 95 11 165 SBA")]
    [TestCase("KNIPEX 95 32 320 kit")]
    [TestCase("KNIPEX Pivot cutter repair kit for 95 32 320")]
    [TestCase("KNIPEX Spare spring for 95 21 165")]
    [TestCase("KNIPEX 95 320 E01")]
    [TestCase("KNIPEX 95 32 315")]
    [TestCase("KNIPEX 95 32 340")]
    [TestCase("KNIPEX 95 32 320 und 95 36 320")]
    [TestCase("Bahco 95 32 320")]
    public void UnknownArticlesVariantsPartsAndPackagesCannotClaimOneBody(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("2 Stück KNIPEX 95 32 320")]
    [TestCase("KNIPEX Pivot cutter repair kit for 95 32 320")]
    [TestCase("KNIPEX Spare spring for 95 21 165")]
    public void RelatedPhysicalOffersKeepTheirOwnScope(string title)
    {
        Assert.That(ResaleCatalog.Match(title), Is.Null);
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True);
    }

    [Test]
    public void ExplicitIncludedCaseDoesNotTurnTheWholeToolIntoAnAccessory() =>
        Assert.That(ResaleCatalog.Match("KNIPEX 95 32 320 mit Koffer")?.Id, Is.EqualTo("knipex-9532320"));
}
