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
