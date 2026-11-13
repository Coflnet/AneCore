using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexElectronicSuperKnipsEsdCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-11-13-knipex-super-knips-esd";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [TestCase("78 03 125 ESD")]
    [TestCase("78 13 125 ESD")]
    [TestCase("78 61 125 ESD")]
    [TestCase("78 71 125 ESD")]
    public void ExactEsdArticleHasItsOwnIdentityWithoutCollapsingTheFamily(string article)
    {
        var id = "knipex-" + article.Replace(" ", "").ToLowerInvariant();
        var row = Catalog.Single(product => product.Id == id);
        Assert.That(row.Model, Is.EqualTo(article));
        Assert.That(row.Source, Is.EqualTo(Source));
        Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "article_number", "length_mm" }));
        Assert.That(row.PossibleAttributes["article_number"], Is.EqualTo(new[] { article }));
        Assert.That(row.PossibleAttributes["length_mm"], Is.EqualTo(new[] { "125" }));
        foreach (var title in row.Aliases.Append("KNIPEX Elektronikzange " + article))
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
    }

    [Test]
    public void ExpandedLoaderAddsExactlyFourDistinctArticlesWithoutBaselineAliasCollisions()
    {
        var added = Catalog.Where(product => product.Source == Source).ToList();
        Assert.That(added, Has.Count.EqualTo(4));
        Assert.That(added.Select(product => product.Id).Distinct().Count(), Is.EqualTo(4));
        var baselineAliases = Catalog.Where(product => product.Source != Source).SelectMany(product => product.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(added.SelectMany(product => product.Aliases).Select(KnownProductMatcher.Normalize).Where(baselineAliases.Contains), Is.Empty);
    }

    [TestCase("78 03 125")]
    [TestCase("78 13 125")]
    [TestCase("78 61 125")]
    [TestCase("78 71 125")]
    public void NewlyVerifiedBaseArticleNeverCollapsesIntoTheEsdVariant(string article)
    {
        var id = "knipex-" + article.Replace(" ", "");
        Assert.That(Matcher.Match("KNIPEX " + article)?.Id, Is.EqualTo(id));
        Assert.That(Matcher.Match("KNIPEX " + article + " ESD")?.Id, Is.EqualTo(id + "esd"));
    }

    [TestCase("KNIPEX Electronic Super Knips")]
    [TestCase("KNIPEX 78 03 140 ESD")]
    [TestCase("KNIPEX 78 03 125 ESDSB")]
    [TestCase("KNIPEX 78 03 125 ESD SB")]
    [TestCase("KNIPEX 7803125ESD/01")]
    [TestCase("KNIPEX 78 03 125 ESD Ersatzfeder")]
    [TestCase("Spare spring for KNIPEX 78 13 125 ESD")]
    [TestCase("KNIPEX 78 03 125 ESD Set")]
    [TestCase("KNIPEX 78 03 125 ESD und 78 13 125 ESD")]
    [TestCase("Bahco 78 03 125 ESD")]
    public void UnverifiedVariantsPartsAndAmbiguousOffersDoNotClaimOneVerifiedEsdTool(string title) =>
        Assert.That(Matcher.Match(title), Is.Null, title);
}
