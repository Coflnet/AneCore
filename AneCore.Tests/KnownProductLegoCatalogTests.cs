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
        Assert.That(sets, Has.Count.EqualTo(7895));
        Assert.That(sets.Select(p => p.Id).Distinct().Count(), Is.EqualTo(sets.Count));
        Assert.That(sets.Select(p => p.Model).Distinct().Count(), Is.EqualTo(sets.Count));
        Assert.That(sets.All(p => p.PossibleAttributes["set_number"].SetEquals(new[] { p.Model })), Is.True);
        Assert.That(sets.Where(p => Matcher.Match(p.Name)?.Id != p.Id).Select(p => p.Id), Is.Empty);
        Assert.That(KnownProductSeed.LoadAll().Any(p => p.Vertical == "lego"), Is.False);
    }

    [TestCase("10280")]
    [TestCase("10281")]
    [TestCase("10309")]
    [TestCase("10311")]
    [TestCase("75304")]
    [TestCase("75305")]
    [TestCase("42115")]
    [TestCase("42143")]
    public void OfficialAdultAgeMetadataRetainsSetIdentityAndOfferBoundaries(string number)
    {
        var id = $"lego-{number}";
        var set = Catalog.Single(p => p.Id == id);
        Assert.That(set.Model, Is.EqualTo(number));
        Assert.That(set.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { number }));
        Assert.That(set.PossibleAttributes["min_age"], Is.EquivalentTo(new[] { "18" }));
        Assert.That(set.Aliases, Is.SupersetOf(new[] { $"LEGO {number}", $"LEGO Set {number}" }));
        foreach (var alias in set.Aliases)
            Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
        Assert.That(Matcher.Match($"LEGO {number} unvollständig"), Is.Null);
        Assert.That(Matcher.Match(number), Is.Null);
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

    [Test]
    public void OfficialInstructionBatchAddsDistinctRetiredSetsConsumedByTheMatcher()
    {
        // Names, set numbers and ages: official LEGO instruction pages; seller phrases are synthetic.
        var sets = Catalog.Where(p => p.Source == "seed:lego-official-instructions-2026-10-28").ToList();
        Assert.That(sets, Has.Count.EqualTo(164));
        foreach (var set in sets)
        {
            Assert.That(Catalog.Count(p => p.Id == set.Id || p.Vertical == "lego" && p.Model == set.Model), Is.EqualTo(1), set.Id);
            Assert.That(set.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { set.Model }));
            foreach (var alias in set.Aliases)
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(set.Id), alias);
            Assert.That(Matcher.Match($"LEGO Set {set.Model} gebraucht vollständig")?.Id, Is.EqualTo(set.Id));
        }
    }

    [Test]
    public void ResaleInstructionBatchResolvesEveryVerifiedBodyAndPreservesOfferScope()
    {
        var sets = Catalog.Where(p => p.Source == "seed:lego-official-resale-instructions-2026-10-28-r40").ToList();
        Assert.That(sets, Has.Count.EqualTo(186));
        Assert.Multiple(() =>
        {
            foreach (var set in sets)
            {
                Assert.That(set.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "set_number", "min_age" }));
                Assert.That(set.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { set.Model }));
                foreach (var alias in set.Aliases)
                    Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(set.Id), alias);
                Assert.That(Matcher.Match($"LEGO Set {set.Model} gebraucht vollständig")?.Id, Is.EqualTo(set.Id));
                Assert.That(Matcher.Match($"LEGO {set.Model} Bauanleitung"), Is.Null);
                Assert.That(Matcher.Match($"LEGO {set.Model} nur OVP"), Is.Null);
                Assert.That(Matcher.Match($"LEGO {set.Model} unvollständig"), Is.Null);
                Assert.That(Matcher.Match(set.Model), Is.Null);
            }
        });
    }

    [TestCase("LEGO 41168", "lego-41168")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation", "lego-41168")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation gebraucht", "lego-41168")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation mit Box", "lego-41168")]
    [TestCase("LEGO 70618 Destiny's Bounty", "lego-70618")]
    [TestCase("LEGO 71705 Destiny's Bounty", "lego-71705")]
    [TestCase("LEGO 41148 Elsa's Magical Ice Palace", "lego-41148")]
    [TestCase("LEGO 43172 Elsa's Magical Ice Palace", "lego-43172")]
    [TestCase("LEGO 71737 X-1 Ninja Charger", "lego-71737")]
    [TestCase("LEGO 71765 Ninja Ultra Combo Mech", "lego-71765")]
    [TestCase("LEGO 70146 Flying Phoenix Fire Temple", "lego-70146")]
    [TestCase("LEGO 70226 Mammoth's Frozen Stronghold", "lego-70226")]
    public void RetiredThemeBodiesKeepPrintedNumbersEvenWhenOfficialNamesRepeat(string title, string id) =>
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("LEGO 70618 Destiny's Bounty und 71705")]
    [TestCase("LEGO 41168 Box")]
    [TestCase("Box LEGO 41168 Elsa's Jewelry Box Creation")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation Box")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation Bauanleitung")]
    [TestCase("LEGO 41168 Elsa's Jewelry Box Creation nur OVP")]
    [TestCase("LEGO 41148 Elsa's Magical Ice Palace 43172")]
    [TestCase("LEGO 71765 Ninja Ultra Combo Mech bundle")]
    [TestCase("LEGO 71737 X-1 Ninja Charger charger")]
    [TestCase("LEGO 70146 Ersatzteile")]
    [TestCase("LEGO 70226 kompatibel")]
    [TestCase("LEGO 70618-2")]
    [TestCase("LEGO Destiny's Bounty")]
    [TestCase("Lego ninjago QG des ninjas")]
    [TestCase("Lego reine des neige")]
    [TestCase("Lego Chima")]
    [TestCase("Bauanleitung Lego Star Wars Nr 75000")]
    public void NewThemeCoverageDoesNotGuessSetsOrPoolObservedInstructionAndUnspecificOffers(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);

    [TestCase("LEGO 42056 Porsche 911 GT3 RS", "lego-42056")]
    [TestCase("LEGO 42083 Bugatti Chiron", "lego-42083")]
    [TestCase("LEGO 42100 Liebherr R 9800 Excavator", "lego-42100")]
    [TestCase("LEGO 42043 Mercedes-Benz Arocs 3245", "lego-42043")]
    [TestCase("LEGO 42111 Dom's Dodge Charger", "lego-42111")]
    [TestCase("LEGO 21318 Tree House", "lego-21318")]
    [TestCase("LEGO 21028 New York City, USA", "lego-21028")]
    public void RetiredInstructionModelsResolveExactNumberAndOfficialName(string title, string id) =>
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("42056")]
    [TestCase("Porsche 911 GT3 RS")]
    [TestCase("LEGO 42056-2")]
    [TestCase("LEGO 42056 Bauanleitung")]
    [TestCase("LEGO 42100 nur OVP")]
    [TestCase("LEGO 21318 unvollständig")]
    [TestCase("LEGO 42056 und 42100")]
    [TestCase("LEGO 42083 Ersatzteile")]
    public void RetiredInstructionCatalogueDoesNotTurnPartsVariantsOrLotsIntoWholeSets(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);

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

    [TestCase("100% completo", true)]
    [TestCase("100% Completo", true)]
    [TestCase("100% COMPLETE", true)]
    [TestCase("100% VOLLSTÄNDIG", true)]
    [TestCase("80% Completo", false)]
    [TestCase("99% COMPLETE", false)]
    [TestCase("not 100% COMPLETE", false)]
    public void DirectCatalogueMatchingRespectsCompletenessBeforeFoldingCase(string claim, bool complete)
    {
        var product = Matcher.Match($"LEGO 75895 1974 Porsche 911 Turbo 3.0 ({claim})");
        Assert.That(product?.Id, Is.EqualTo(complete ? "lego-75895" : null));
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
