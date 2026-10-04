using Coflnet.Ane;

namespace AneCore.Tests;

/// <summary>Round twenty-six: the case pages of games (relation key, never a language edition) and the list of unidentified game listings.</summary>
[TestFixture]
public class GameCaseAndUnidentifiedTests
{
    private static Product CasePage(string caseOf = "playstation-2-def-jam-fight-ny-used") => new()
    {
        SeoId = "playstation-2-def-jam-fight-ny-case-used",
        Name = "Def Jam Fight Ny (PlayStation 2) – empty case, no game",
        Attributes = new Dictionary<string, string>
        {
            ["platform"] = "PlayStation 2", ["game_title"] = "Def Jam Fight Ny", ["product_kind"] = "game_case", ["case_of"] = caseOf, ["game_id"] = "Q1", ["game_platform"] = "PlayStation 2",
        },
    };

    [Test]
    public void ACasePage_HasTheCaseRelationKey_AndNeverTheGameKey()
    {
        var product = CasePage();
        Assert.That(Product.IsGameCase(product), Is.True);
        Assert.That(ProductRelation.CaseKey("playstation-2-def-jam-fight-ny-used"), Is.EqualTo("case:playstation-2-def-jam-fight-ny-used"));
        Assert.That(ProductTableService.CaseRelationKeyOf(product), Is.EqualTo("case:playstation-2-def-jam-fight-ny-used"));
        // even with a game id on it (a case is no language edition of the game)
        Assert.That(ProductTableService.GameRelationKeyOf(product), Is.Null);
        Assert.That(ProductTableService.RelationKeyOf(product), Is.EqualTo("case:playstation-2-def-jam-fight-ny-used"));
    }

    [Test]
    public void AGamePage_KeepsItsGameKey_AndIsNoCase()
    {
        var game = new Product { SeoId = "playstation-2-def-jam-fight-ny-used", Attributes = new Dictionary<string, string> { ["game_id"] = "Q1", ["platform"] = "PlayStation 2" } };
        Assert.That(Product.IsGameCase(game), Is.False);
        Assert.That(ProductTableService.CaseRelationKeyOf(game), Is.Null);
        Assert.That(ProductTableService.GameRelationKeyOf(game), Is.EqualTo("game:Q1:PlayStation 2"));
        Assert.That(ProductTableService.RelationKeyOf(game), Is.EqualTo("game:Q1:PlayStation 2"));
    }

    [Test]
    public void ACasePageWithoutCaseOf_HasNoRelationKey()
    {
        var product = CasePage();
        product.Attributes!.Remove("case_of");
        Assert.That(ProductTableService.CaseRelationKeyOf(product), Is.Null);
        Assert.That(Product.IsGameCase(new Product()), Is.False);
    }

    [Test]
    public void TheCaseKind_IsNoColumnOfTheProductsTable()
    {
        // a property on Product would be mapped as a column; the check is a static method
        Assert.That(typeof(Product).GetProperties().Select(p => p.Name), Does.Not.Contain("IsGameCase"));
    }

    [Test]
    public void AnUnidentifiedGame_IsKeyedByPlatformSeriesAndListing()
    {
        Assert.That(UnidentifiedGame.ListingKey((int)Platform.Vinted, "v-4711"), Is.EqualTo($"{(int)Platform.Vinted}:v-4711"));
        Assert.That(UnidentifiedGame.TtlSeconds, Is.EqualTo(90 * 24 * 3600));
        Assert.That(new UnidentifiedGame().IdentifyStage, Is.EqualTo(UnidentifiedGame.StageDescription));
        Assert.That(new[] { UnidentifiedGame.StageTitle, UnidentifiedGame.StageDescription, UnidentifiedGame.StagePhoto }, Is.EqualTo(new[] { "title", "description", "photo" }));
        // the Cassandra driver cannot map enums: the marketplace is an int
        Assert.That(typeof(UnidentifiedGame).GetProperty(nameof(UnidentifiedGame.ListingPlatform))!.PropertyType, Is.EqualTo(typeof(int)));
    }
}
