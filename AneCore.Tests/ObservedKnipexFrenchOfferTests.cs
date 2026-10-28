using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class ObservedKnipexFrenchOfferTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);
    private const string ObservedTitle = "Knipex pince becs longs et effiles  26 12 200";

    [Test]
    public void ObservedFrenchArticleReusesExistingIdentityAndLength()
    {
        var row = Catalog.Single(p => p.Brand == "KNIPEX" && p.Model == "26 12 200");
        Assert.That(row.Id, Is.EqualTo("knipex-2612200"));
        Assert.That(row.PossibleAttributes["length_mm"], Is.EquivalentTo(new[] { "200" }));
        Assert.That(Matcher.Match(ObservedTitle)?.Id, Is.EqualTo(row.Id));
        Assert.That(ResaleCatalog.Match(ObservedTitle)?.Id, Is.EqualTo(row.Id));
    }

    [TestCase(" T")]
    [TestCase(" S02")]
    [TestCase(" kit")]
    [TestCase(" coffret")]
    [TestCase(" 26 22 200")]
    public void VariantPackageAndSecondArticleCannotClaimTheObservedSingleTool(string suffix) =>
        Assert.That(ResaleCatalog.Match(ObservedTitle + suffix), Is.Null);

    [TestCase("Knipex pince becs longs et effiles")]
    [TestCase("Knipex pince becs longs et effiles 26 22 200")]
    public void DescriptorAloneOrAnotherArticleCannotClaim2612200(string title) =>
        Assert.That(Matcher.Match(title)?.Id, Is.Not.EqualTo("knipex-2612200"));

    [TestCase("KNIPEX Flachrundzange 26 12 200", "knipex-2612200")]
    [TestCase("Knipex pince becs longs et effiles 2612200", "knipex-2612200")]
    [TestCase("Bahco douille 7800SM-13", "bahco-7800sm-13")]
    public void DescriptorsMaySeparateTheExactKnownBrandAndArticle(string title, string id)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));
    }

    [TestCase("Knipex pince ARTICLE_NUMBER")]
    [TestCase("Knipex pince 26 12 20")]
    [TestCase("Knipex pince 26 12 2000")]
    [TestCase("Knipex pince 26 12 200foo")]
    [TestCase("Knipex pince 26 12 200/1")]
    [TestCase("Knipex pince 26 12 200,5")]
    [TestCase("Bahco pince 26 12 200")]
    [TestCase("Knipex Bahco pince 26 12 200")]
    [TestCase("Knipex pince Bahco 26 12 200")]
    [TestCase("Knipex pince 26 12 200 26 22 200")]
    [TestCase("Knipex pince 26 12 200 et 26 11 200")]
    [TestCase("Knipex coffret pince 26 12 200")]
    [TestCase("Knipex Ersatzbacke pour pince 26 12 200")]
    [TestCase("Bahco douille 7800SM-13FOO")]
    [TestCase("Bahco douille 6700SM-4.8")]
    public void ExactArticleRematchCannotGuessOrPromoteConflictingOffers(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [Test]
    public void ExtractedBrandAndKnownCategoryStillVetoTheRematch()
    {
        Assert.That(Matcher.Match(ObservedTitle, brand: "Bahco"), Is.Null);
        Assert.That(Matcher.Match(ObservedTitle, categoryPath: new[] { "Bekleidung & Accessoires", "Bekleidung" }), Is.Null);
    }

    [Test]
    public void DuplicateVerifiedBrandArticleCannotSelectOneProduct()
    {
        var row = Catalog.Single(p => p.Id == "knipex-2612200");
        var duplicate = new KnownProduct
        {
            Id = "test-conflicting-article", Brand = row.Brand, Model = row.Model,
            Categories = row.Categories, PossibleAttributes = row.PossibleAttributes,
            Aliases = new(StringComparer.OrdinalIgnoreCase) { "conflicting exact article" }
        };
        Assert.That(new KnownProductMatcher(new[] { row, duplicate }).Match(ObservedTitle), Is.Null);
    }

    [TestCase("Knipex pince à sertir  cosses isolées 0,5 - 6,0mm")]
    [TestCase("Knipex pince becs longs et effiles  26 12 200")]
    [TestCase("Knipex tänger fri frakt")]
    [TestCase("Knipex kabelsax 95 32 320 papegoja")]
    public void ObservedLocalizedPrimaryToolsReachPhysicalOfferAdmission(string title) =>
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.True, title);

    [TestCase("Używana walizka narzędziowa Knipex BIG Twin Move dla elektryków")]
    [TestCase("Sprzedam prywatnie widoczny na zdjęciach zestaw narzędzi i osprzętu renomowanych marek takich jak Bosch, Milwaukee, Format, Jokari oraz Knipex.")]
    [TestCase("UnknownBrand pince à sertir")]
    [TestCase("Knipex pince Bedienungsanleitung")]
    [TestCase("Adapter für Knipex pince")]
    public void MentionedBrandsCasesMediaAndAccessoriesCannotInventAPrimaryTool(string title) =>
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False, title);

    [TestCase("Knipex kabelsax Kabel")]
    [TestCase("Knipex kabelsax Ladegerät")]
    [TestCase("Knipex kabelsax Adapter")]
    [TestCase("Adapter für Knipex kabelsax")]
    public void CableCutterNameDoesNotHideASeparateAccessory(string title) =>
        Assert.That(ResaleCatalog.IsHandToolOffer(title), Is.False, title);

    [Test]
    public void CableCutterWithIncludedBagRemainsAnIndependentTool() =>
        Assert.That(ResaleCatalog.IsHandToolOffer("Knipex kabelsax mit Tasche"), Is.True);
}
