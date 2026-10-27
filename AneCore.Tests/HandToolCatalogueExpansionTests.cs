using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class HandToolCatalogueExpansionTests
{
    private static readonly List<KnownProduct> Products = KnownProductSeed.LoadAll(includeExpandedCatalog: true).ToList();
    private static readonly KnownProductMatcher Matcher = new(Products);

    [TestCase("knipex-8604100", "100", "pliers_wrench")]
    [TestCase("knipex-8700100", "100", "water_pump_pliers")]
    [TestCase("knipex-8602180", "180", "pliers_wrench")]
    [TestCase("knipex-8602250", "250", "pliers_wrench")]
    [TestCase("knipex-8703180", "180", "water_pump_pliers")]
    [TestCase("knipex-8703250", "250", "water_pump_pliers")]
    [TestCase("bahco-8223", "210", "water_pump_pliers")]
    [TestCase("bahco-8224", "250", "water_pump_pliers")]
    [TestCase("bahco-8225", "315", "water_pump_pliers")]
    [TestCase("wera-05003500001", "152", "ratchet")]
    [TestCase("wera-05003550001", "199", "ratchet")]
    [TestCase("wera-05003600001", "277", "ratchet")]
    public void VerifiedArticlesAndAliasesKeepTheirOwnToolAndLength(string id, string length, string type)
    {
        var product = Products.Single(p => p.Id == id);
        Assert.That(product.PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { length }));
        Assert.That(product.PossibleAttributes["tool_type"], Is.EquivalentTo(new[] { type }));
        Assert.That(product.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "hand_tool" }));
        Assert.That(product.PossibleAttributes["article_number"], Has.Count.EqualTo(1));
        Assert.That(product.PossibleAttributes.Keys, Does.Not.Contain("width_mm"));
        foreach (var alias in product.Aliases)
        {
            Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
            Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(id), alias);
        }
    }

    [TestCase("KNIPEX Cobra")]
    [TestCase("KNIPEX Zangenschlüssel")]
    [TestCase("Wera Zyklop")]
    [TestCase("Wera 8000")]
    [TestCase("Wera 8000 D")]
    [TestCase("Wera 8003 A")]
    [TestCase("Wera 8004 B")]
    [TestCase("Wera 8007 C")]
    [TestCase("Bahco Wasserpumpenzange")]
    [TestCase("KNIPEX 86 02 180 T")]
    [TestCase("Bahco 8224 C")]
    [TestCase("Bahco 8224C")]
    [TestCase("KNIPEX 86 04 100 SB")]
    [TestCase("KNIPEX 87 03 250 SBA")]
    [TestCase("Wera 8000 A SB")]
    public void UnspecifiedFamiliesAndUnverifiedArticleVariantsStayUnknown(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    // Exact manufacturer-proven packaging articles retain the physical tool's existing identity.
    [TestCase("KNIPEX 86 04 100 BK", "knipex-8604100")]
    [TestCase("KNIPEX 87 00 100 BK", "knipex-8700100")]
    [TestCase("KNIPEX 86 02 180 SB", "knipex-8602180")]
    [TestCase("KNIPEX 86 02 250 SB", "knipex-8602250")]
    [TestCase("KNIPEX 87 03 250 SB", "knipex-8703250")]
    [TestCase("KNIPEX 86 02 180 BK", "knipex-8602180")]
    [TestCase("KNIPEX 86 02 250 BK", "knipex-8602250")]
    [TestCase("KNIPEX 87 03 180 BK", "knipex-8703180")]
    [TestCase("KNIPEX 87 03 250 BK", "knipex-8703250")]
    [TestCase("Bahco 8223 IP", "bahco-8223")]
    [TestCase("Bahco 8224 IP", "bahco-8224")]
    [TestCase("Bahco 8225 IP", "bahco-8225")]
    public void VerifiedPackagingArticlesKeepTheCanonicalToolBody(string title, string id)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
    }

    [TestCase("Set Wera 8000 A")]
    [TestCase("Wera 8000 A Set")]
    [TestCase("Set KNIPEX 86 02 180")]
    [TestCase("Case KNIPEX 86 04 100")]
    [TestCase("Wera 8000 A Zyklop 152 mm Set")]
    [TestCase("Bahco 8224 Wasserpumpenzange 250 mm Satz")]
    [TestCase("KNIPEX 86 02 180 Länge 180 mm T")]
    [TestCase("Wera 8000 A 152 mm im Koffer")]
    [TestCase("Koffer KNIPEX 86 04 100")]
    [TestCase("KNIPEX 86 04 100 Kit")]
    [TestCase("Bahco 8224 Satz")]
    [TestCase("Bahco 8224 T6")]
    [TestCase("Bahco 8224/T7")]
    [TestCase("Bahco 8224 T8")]
    [TestCase("Bahco 8224 T 6")]
    [TestCase("Bahco 8224 250 mm T7")]
    [TestCase("Bahco 8224 Wasserpumpenzange T 8")]
    [TestCase("Bahco 8224 8225")]
    [TestCase("KNIPEX 86 02 180 86 02 250")]
    [TestCase("KNIPEX 8602180 86 02 250")]
    [TestCase("KNIPEX 86 02 180 8602250")]
    [TestCase("KNIPEX 8602180 / 8602250")]
    [TestCase("Wera 05003500001 8000 B")]
    [TestCase("Wera 8000 A 05003550001")]
    [TestCase("Wera 05003550001 8000 C")]
    [TestCase("Wera 05003500001 05003550001")]
    [TestCase("Wera 8000 A 8000 B")]
    [TestCase("Wera 8000 A + KNIPEX 86 04 100")]
    [TestCase("Ersatzbacken für KNIPEX 86 04 100")]
    [TestCase("Adapter for Wera 8000 A")]
    [TestCase("Wera 8000 A Bedienungsanleitung")]
    [TestCase("Bahco 8224 gesucht")]
    [TestCase("Set KNIPEX 86 04 100 BK")]
    [TestCase("KNIPEX 86 02 180 SB Set")]
    [TestCase("Koffer KNIPEX 87 03 250 SB")]
    [TestCase("Bahco 8224 IP Satz")]
    [TestCase("2x Bahco 8224 IP")]
    [TestCase("Bahco 8224 IP 2 Stück")]
    [TestCase("2 Stück KNIPEX 86 04 100 BK")]
    [TestCase("KNIPEX 86 02 180 SB 2er Set")]
    [TestCase("Bahco 8224 IP / 8225 IP")]
    public void SetsPartsAndMultipleArticlesAreNotOneToolBody(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null, title);

    [TestCase("Bahco 8224 mit Ersatzbacken", "bahco-8224")]
    [TestCase("KNIPEX 86 04 100 mit Zubehör", "knipex-8604100")]
    [TestCase("Wera 8000 A mit Zubehör", "wera-05003500001")]
    [TestCase("KNIPEX 86 04 100 mit Anleitung", "knipex-8604100")]
    [TestCase("Wera 8000 A with manual", "wera-05003500001")]
    [TestCase("KNIPEX 86 04 100 100 x 31 x 10 mm", "knipex-8604100")]
    [TestCase("1 Stück Bahco 8224 IP", "bahco-8224")]
    [TestCase("1x KNIPEX 86 04 100 BK", "knipex-8604100")]
    [TestCase("KNIPEX 8602180 86 02 180", "knipex-8602180")]
    [TestCase("KNIPEX 86 02 180 8602180", "knipex-8602180")]
    [TestCase("Bahco 8224 8224", "bahco-8224")]
    [TestCase("Wera 8000 A 05003500001", "wera-05003500001")]
    [TestCase("Wera 05003500001 8000 A", "wera-05003500001")]
    [TestCase("knipex 86 04 100", "knipex-8604100")]
    [TestCase("KNIPEX 8604100", "knipex-8604100")]
    [TestCase("BAHCO 8224", "bahco-8224")]
    [TestCase("wera 8000 a", "wera-05003500001")]
    public void IncludedExtrasRepeatedSameArticleAndCaseVariantsKeepTheTool(string title, string id)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
}
