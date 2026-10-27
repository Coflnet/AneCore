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
        Assert.That(sets, Has.Count.EqualTo(7282));
        Assert.That(sets.Select(p => p.Id).Distinct().Count(), Is.EqualTo(sets.Count));
        Assert.That(sets.Select(p => p.Model).Distinct().Count(), Is.EqualTo(sets.Count));
        Assert.That(sets.All(p => p.PossibleAttributes["set_number"].SetEquals(new[] { p.Model })), Is.True);
        Assert.That(sets.Where(p => Matcher.Match(p.Name)?.Id != p.Id).Select(p => p.Id), Is.Empty);
        Assert.That(KnownProductSeed.LoadAll().Any(p => p.Vertical == "lego"), Is.False);
    }

    [Test]
    public void RetiredSpeedChampionsBatchResolvesEverySetNumber()
    {
        var sets = Catalog.Where(p => p.Source == "seed:lego-official-speed-champions-2026-10").ToList();
        var numbers = Enumerable.Range(75870, 26).Append(75899).Concat(Enumerable.Range(75908, 6)).Select(n => n.ToString()).ToList();
        Assert.That(sets.Select(p => p.Model), Is.EquivalentTo(numbers));
        foreach (var set in sets)
        {
            Assert.That(set.Aliases, Is.SupersetOf(new[] { $"LEGO {set.Model}", $"LEGO Set {set.Model}" }));
            Assert.That(Matcher.Match($"LEGO Set {set.Model} gebraucht vollständig")?.Id, Is.EqualTo(set.Id));
        }
    }

    [TestCase("LEGO 75876 Porsche 919 Hybrid and 917K Pit Lane", "lego-75876")]
    [TestCase("LEGO 75881 2016 Ford GT & 1966 Ford GT40", "lego-75881")]
    [TestCase("LEGO 75884 1968 Ford Mustang Fastback", "lego-75884")]
    [TestCase("LEGO 75886 Ferrari 488 GT3 “Scuderia Corsa”", "lego-75886")]
    [TestCase("LEGO 75887 Porsche 919 Hybrid", "lego-75887")]
    [TestCase("LEGO 75888 Porsche 911 RSR and 911 Turbo 3.0", "lego-75888")]
    [TestCase("LEGO 75893 2018 Dodge Challenger SRT Demon and 1970 Dodge Charger R/T", "lego-75893")]
    [TestCase("LEGO 75894 1967 Mini Cooper S Rally and 2018 MINI John Cooper Works Buggy", "lego-75894")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0", "lego-75895")]
    [TestCase("LEGO 75908 458 Italia GT2", "lego-75908")]
    [TestCase("LEGO 75910 Porsche 918 Spyder", "lego-75910")]
    [TestCase("LEGO 75870 Chevrolet Corvette Z06", "lego-75870")]
    [TestCase("LEGO 75878 Bugatti Chiron", "lego-75878")]
    [TestCase("LEGO 75899 LaFerrari", "lego-75899")]
    [TestCase("LEGO 75909 McLaren P1", "lego-75909")]
    [TestCase("LEGO 75913 F14 T & Scuderia Ferrari Truck", "lego-75913")]
    [TestCase("LEGO42115 neu OVP", "lego-42115")]
    [TestCase("LEGO Set 10221 gebraucht vollständig", "lego-10221")]
    [TestCase("LEGO 75192 Millennium Falcon", "lego-75192")]
    [TestCase("LEGO 75192-1 Millennium Falcon", "lego-75192")]
    [TestCase("LEGO 75192 mit Anleitung", "lego-75192")]
    [TestCase("LEGO 42115 mit Box", "lego-42115")]
    [TestCase("LEGO 75192 mit Originalverpackung und Anleitung", "lego-75192")]
    [TestCase("LEGO 10294 Titanic", "lego-10294")]
    [TestCase("LEGO 10316 Rivendell gebraucht", "lego-10316")]
    [TestCase("LEGO Set 10305 vollständig", "lego-10305")]
    [TestCase("LEGO 10333 Barad-dûr", "lego-10333")]
    [TestCase("LEGO 21335 Motorized Lighthouse", "lego-21335")]
    [TestCase("LEGO 7592 Buzz Lightyear", "lego-7592")]
    [TestCase("LEGO 8200 Lightning McQueen", "lego-8200")]
    public void ExactBrandAndSetNumberMatch(string title, string expected)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expected));
    }

    [TestCase("LEGO 75893 2018 Dodge Challenger SRT Demon and 1970 Dodge Charger R/T charger")]
    [TestCase("charger LEGO 75893 2018 Dodge Challenger SRT Demon and 1970 Dodge Charger R/T")]
    [TestCase("LEGO 75893 2018 Dodge Challenger SRT Demon and 1970 Dodge Charger R/T Hülle")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0 und 75878")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0 und 928")]
    [TestCase("928 LEGO 75895 1974 Porsche 911 Turbo 3.0")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0 9999999")]
    [TestCase("LEGO 75895-2 1974 Porsche 911 Turbo 3.0")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0 nur OVP")]
    [TestCase("LEGO 75895 1974 Porsche 911 Turbo 3.0 Bauanleitung")]
    [TestCase("LEGO 75895 Porsche 911 Turbo 3.0")]
    [TestCase("75878")]
    [TestCase("Bugatti Chiron")]
    [TestCase("LEGO 75878 nur OVP")]
    [TestCase("LEGO 75878 Bauanleitung")]
    [TestCase("LEGO 75878 und 75870")]
    [TestCase("LEGO 75878-2")]
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
    [TestCase("Lightailing LED Kit voor LEGO Lord of the Rings Rivendell 10316")]
    [TestCase("LEGO 10316 Lighting Kit")]
    [TestCase("LEGO 10316 Light Kit")]
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
