using Coflnet.Ane;

namespace AneCore.Tests;

public class CategoryScopeComicsTests
{
    [TestCase("284")]
    [TestCase("Comics")]
    public void ComicLeafIsActivelyCrawledWithoutDroppingUnnumberedOffers(string category)
    {
        var scope = CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Kleinanzeigen, category)!;
        Assert.That(scope.Vertical!.Key, Is.EqualTo("comics"));
        Assert.That(scope.Vertical.IsActive, Is.True);
        Assert.That(scope.MatchesTitle("Batman Sammlung"), Is.True);
    }

    // Native IDs from the captured Marktplaats /v/boeken/stripboeken and /strips-comics rows.
    [TestCase("227")]
    [TestCase("1459")]
    public void MarktplaatsComicLeavesKeepGenericComicTitles(string category)
    {
        var scope = CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Marktplaats, category)!;
        Assert.That(scope.Vertical!.Key, Is.EqualTo("comics"));
        Assert.That(scope.Vertical.IsActive, Is.True);
        Assert.That(scope.MatchesTitle("PEP weekbladen"), Is.True);
    }

    [TestCase("1447")]
    [TestCase("1125")]
    [TestCase("896")]
    public void OtherMarktplaatsLeavesDoNotBecomeComics(string category) =>
        Assert.That(CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Marktplaats, category)?.Vertical?.Key, Is.Not.EqualTo("comics"));

    [Test]
    public void BroaderBooksCategoryDoesNotBecomeAnUnboundedCrawl() =>
        Assert.That(CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Kleinanzeigen, "76")!.IsOutOfScope, Is.True);

    [Test]
    public void InternalComicLeafDoesNotClaimAllPrintedBooks()
    {
        var categories = new UnifiedCategoryService();
        Assert.That(categories.ResolveCategoryPath("ane-comics"), Is.EqualTo(new[] { "783", "784", "543543", "ane-comics" }));
        var index = CategoryScopeCatalog.Shared.BuildUnifiedIndex(categories);
        Assert.That(index.Resolve(new[] { "Medien", "Bücher", "Gedruckte Bücher", "Comics" })!.Vertical!.Key, Is.EqualTo("comics"));
        Assert.That(index.Resolve(new[] { "Medien", "Bücher", "Gedruckte Bücher" })?.Vertical?.Key, Is.Not.EqualTo("comics"));
    }
}
