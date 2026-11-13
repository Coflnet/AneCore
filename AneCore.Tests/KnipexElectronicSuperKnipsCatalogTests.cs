using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexElectronicSuperKnipsCatalogTests
{
    private const string Source = "seed:manufacturer-verified-2026-11-13-knipex-super-knips-base";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [TestCase("78 03 125")]
    [TestCase("78 06 125")]
    [TestCase("78 13 125")]
    [TestCase("78 23 125")]
    [TestCase("78 31 125")]
    [TestCase("78 41 125")]
    [TestCase("78 61 125")]
    [TestCase("78 71 125")]
    [TestCase("78 81 125")]
    [TestCase("78 91 125")]
    public void CompleteManufacturerArticleHasItsOwnIdentityWithoutInferredDimensions(string article)
    {
        var id = "knipex-" + article.Replace(" ", "");
        var product = Catalog.Single(row => row.Id == id);
        Assert.That(product.Source, Is.EqualTo(Source));
        Assert.That(product.Model, Is.EqualTo(article));
        Assert.That(product.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "product_kind", "article_number" }));
        Assert.That(product.PossibleAttributes["article_number"], Is.EqualTo(new[] { article }));
        foreach (var title in product.Aliases.Append("KNIPEX Elektronikzange " + article))
            Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
    }

    [Test]
    public void TenReviewedArticlesAddNoDuplicateIdentitiesOrBaselineAliases()
    {
        var added = Catalog.Where(row => row.Source == Source).ToList();
        Assert.That(added, Has.Count.EqualTo(10));
        Assert.That(added.Select(row => row.Id).Distinct().ToList(), Has.Count.EqualTo(10));
        var baselineAliases = Catalog.Where(row => row.Source != Source).SelectMany(row => row.Aliases)
            .Select(KnownProductMatcher.Normalize).ToHashSet();
        Assert.That(added.SelectMany(row => row.Aliases).Select(KnownProductMatcher.Normalize).Where(baselineAliases.Contains), Is.Empty);
    }

    [Test]
    public void VdeArticleIsNotAnUnmarkedBaseOrEsdAlias()
    {
        Assert.That(Matcher.Match("KNIPEX 78 06 125 VDE")?.Id, Is.EqualTo("knipex-7806125"));
        Assert.That(Matcher.Match("KNIPEX 78 03 125 VDE"), Is.Null);
        Assert.That(Matcher.Match("KNIPEX 78 06 125 ESD"), Is.Null);
    }

    [TestCase("KNIPEX Electronic Super Knips")]
    [TestCase("KNIPEX Electronic Super Knips VDE")]
    [TestCase("KNIPEX 78 03 140")]
    [TestCase("KNIPEX 78 23 125 ESD")]
    [TestCase("KNIPEX 78 03 125 ESDSB")]
    [TestCase("KNIPEX 78 03 125 SB")]
    [TestCase("KNIPEX 7803125/01")]
    [TestCase("KNIPEX 78 03 125 T")]
    [TestCase("KNIPEX 78 03 125 Ersatzfeder")]
    [TestCase("Spare spring for KNIPEX 78 13 125")]
    [TestCase("KNIPEX 78 03 125 Set")]
    [TestCase("KNIPEX 78 03 125 und 78 13 125")]
    [TestCase("Bahco 78 03 125")]
    public void UnreviewedVariantsSparesAndAmbiguousOffersDoNotBecomeOneWholeArticle(string title) =>
        Assert.That(Matcher.Match(title), Is.Null, title);
}
