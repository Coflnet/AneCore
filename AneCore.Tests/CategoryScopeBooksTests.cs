using Coflnet.Ane;

namespace AneCore.Tests;

public class CategoryScopeBooksTests
{
    [TestCase(Platform.Gumtree, "Books")]
    [TestCase(Platform.Kleinanzeigen, "76")]
    [TestCase(Platform.Kleinanzeigen, "Bücher & Zeitschriften")]
    public void PhysicalBookLeaves_AreActivelyCrawled(Platform platform, string category)
    {
        var scope = CategoryScopeCatalog.Shared.ResolvePlatform(platform, category);
        Assert.That(scope?.Vertical?.Key, Is.EqualTo("books"));
        Assert.That(scope!.Vertical!.IsActive, Is.True);
    }

    [TestCase("Music, Films, Books & Games")]
    [TestCase("Books, Comics & Magazines")]
    public void AggregateGumtreeParents_AreNotActivated(string category) =>
        Assert.That(CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Gumtree, category)?.Vertical?.Key, Is.Not.EqualTo("books"));

    [Test]
    public void PrintedBooksAndStationery_DoNotActivateEbooksOrAudiobooks()
    {
        var index = CategoryScopeCatalog.Shared.BuildUnifiedIndex(new UnifiedCategoryService());
        Assert.That(index.Resolve(["Medien", "Bücher", "Gedruckte Bücher"])?.Vertical?.Key, Is.EqualTo("books"));
        Assert.That(index.Resolve(["Bürobedarf", "Schreibwaren", "Papierprodukte", "Notizbücher & Notizblöcke"])?.Vertical?.Key, Is.EqualTo("books"));
        Assert.That(index.Resolve(["Medien", "Bücher", "E-Books"])?.Vertical?.Key, Is.Not.EqualTo("books"));
        Assert.That(index.Resolve(["Medien", "Bücher", "Audiobücher"])?.Vertical?.Key, Is.Not.EqualTo("books"));
        Assert.That(index.Resolve(["Medien", "Bücher", "Gedruckte Bücher", "Comics"])?.Vertical?.Key, Is.EqualTo("comics"));
    }
}
