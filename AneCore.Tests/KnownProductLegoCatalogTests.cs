using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductLegoCatalogTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void EverySetHasAUniqueIdentityAndResolvesToItself()
    {
        var sets = Catalog.Where(p => p.Vertical == "lego").ToList();
        Assert.That(sets, Has.Count.EqualTo(5630));
        Assert.That(sets.Select(p => p.Id).Distinct().Count(), Is.EqualTo(sets.Count));
        Assert.That(sets.All(p => p.PossibleAttributes["set_number"].SetEquals(new[] { p.Model })), Is.True);
        Assert.That(sets.Where(p => Matcher.Match(p.Name)?.Id != p.Id).Select(p => p.Id), Is.Empty);
        Assert.That(KnownProductSeed.LoadAll().Any(p => p.Vertical == "lego"), Is.False);
    }

    [TestCase("LEGO42115 neu OVP", "lego-42115")]
    [TestCase("LEGO Set 10221 gebraucht vollständig", "lego-10221")]
    [TestCase("LEGO 75192 Millennium Falcon", "lego-75192")]
    [TestCase("LEGO 75192-1 Millennium Falcon", "lego-75192")]
    [TestCase("LEGO 75192 mit Anleitung", "lego-75192")]
    [TestCase("LEGO 42115 mit Box", "lego-42115")]
    [TestCase("LEGO 75192 mit Originalverpackung und Anleitung", "lego-75192")]
    [TestCase("LEGO 10294 Titanic", "lego-10294")]
    public void ExactBrandAndSetNumberMatch(string title, string expected)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expected));
    }

    [TestCase("42115")]
    [TestCase("Lamborghini Sián FKP 37")]
    [TestCase("LEGO 42115 Bauanleitung")]
    [TestCase("LEGO 42115 nur OVP")]
    [TestCase("LEGO 42115 Karton")]
    [TestCase("Nur die Verpackung Lego 75192 Falcon")]
    [TestCase("LEGO 75192 - beleuchtet, inkl. Hangar & Tisch")]
    [TestCase("LEGO 42115 unvollständig")]
    [TestCase("LEGO 42115 ohne Figuren")]
    [TestCase("LEGO 42115 MOC")]
    [TestCase("LEGO 42115 Beleuchtung")]
    [TestCase("LEGO 42115 + 42143")]
    [TestCase("LEGO 42115 und LEGO Set 42143")]
    [TestCase("LEGO 421150")]
    [TestCase("LEGO 42115-2")]
    [TestCase("LEGO42115-2")]
    [TestCase("LEGO 10020")]
    public void AmbiguousNumbersAndScopedOffersDoNotBecomeACompleteSet(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null);
    }

    [Test]
    public void ClothingCategoryCannotClaimTheToy()
    {
        Assert.That(Matcher.Match("Adidas LEGO 42115 Schuhe", categoryPath: new[] { "Bekleidung & Accessoires", "Schuhe" }), Is.Null);
    }
}
