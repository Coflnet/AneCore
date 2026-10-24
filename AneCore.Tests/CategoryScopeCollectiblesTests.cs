using Coflnet.Ane;

namespace AneCore.Tests;

public class CategoryScopeCollectiblesTests
{
    [TestCase(Platform.Kleinanzeigen, "23", "LEGO City 60275", "lego")]
    [TestCase(Platform.Vinted, "1767", "LEGO Technic 42115", "lego")]
    [TestCase(Platform.Willhaben, "5182", "LEGO 75192", "lego")]
    [TestCase(Platform.Kleinanzeigen, "78", "Pink Floyd The Wall Schallplatten", "vinyl_records")]
    [TestCase(Platform.Kleinanzeigen, "Musik & CDs", "Mike & the Mechanics Living Years LP", "vinyl_records")]
    [TestCase(Platform.Vinted, "3041", "Abbey Road", "vinyl_records")]
    [TestCase(Platform.Willhaben, "6462;2978;999001", "Beatles Abbey Road Vinyl", "vinyl_records")]
    public void CollectiblesAreActivelyCrawled(Platform platform, string category, string title, string vertical)
    {
        var scope = CategoryScopeCatalog.Shared.ResolvePlatform(platform, category)!;
        Assert.That(scope.Vertical!.Key, Is.EqualTo(vertical));
        Assert.That(scope.Vertical.IsActive, Is.True);
        Assert.That(scope.MatchesTitle(title), Is.True);
    }

    [TestCase(Platform.Kleinanzeigen, "78", "Beatles Abbey Road CD")]
    [TestCase(Platform.Willhaben, "2978", "Musikkassetten Sammlung")]
    [TestCase(Platform.Kleinanzeigen, "23", "Holzeisenbahn")]
    public void BroadCategoriesKeepTheirTitleGate(Platform platform, string category, string title) =>
        Assert.That(CategoryScopeCatalog.Shared.ResolvePlatform(platform, category)!.MatchesTitle(title), Is.False);
}
