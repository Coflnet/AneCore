using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoCityTechnicCatalogTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void EveryManufacturerVerifiedSupplementResolvesItsAliasesAndKeepsOfferBoundaries()
    {
        var sets = Catalog.Where(p => p.Source == "seed:lego-official-city-technic-instructions-2026-10-29-r41").ToList();
        Assert.That(sets, Has.Count.EqualTo(122));
        Assert.Multiple(() =>
        {
            foreach (var set in sets)
            {
                Assert.That(Catalog.Count(p => p.Id == set.Id || p.Vertical == "lego" && p.Model == set.Model), Is.EqualTo(1), set.Id);
                Assert.That(set.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "set_number", "min_age" }), set.Id);
                Assert.That(set.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { set.Model }), set.Id);
                Assert.That(set.Aliases, Has.Count.EqualTo(3), set.Id);
                foreach (var alias in set.Aliases)
                {
                    Assert.That(alias, Does.StartWith("LEGO ").And.Contain(set.Model));
                    Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(set.Id), alias);
                }
                Assert.That(Matcher.Match($"LEGO Set {set.Model} gebraucht vollständig")?.Id, Is.EqualTo(set.Id));
                Assert.That(Matcher.Match($"LEGO {set.Model} Bauanleitung"), Is.Null);
                Assert.That(Matcher.Match($"LEGO {set.Model} nur OVP"), Is.Null);
                Assert.That(Matcher.Match($"LEGO {set.Model} unvollständig"), Is.Null);
                Assert.That(Matcher.Match(set.Model), Is.Null);
            }
        });
    }

    [TestCase("LEGO 10194 Emerald Night", "lego-10194")]
    [TestCase("LEGO 10254 Winter Holiday Train", "lego-10254")]
    [TestCase("LEGO 10277 Crocodile Locomotive", "lego-10277")]
    [TestCase("LEGO 42030 Remote-Controlled VOLVO L350F Wheel Loader", "lego-42030")]
    [TestCase("LEGO 42028 Bulldozer", "lego-42028")]
    [TestCase("LEGO 60074 Bulldozer", "lego-60074")]
    [TestCase("LEGO 60051 High-speed Passenger Train", "lego-60051")]
    [TestCase("LEGO 60052 Cargo Train", "lego-60052")]
    [TestCase("LEGO 60198 Cargo Train", "lego-60198")]
    [TestCase("LEGO 60167 Coast Guard Head Quarters gebraucht vollständig mit Box", "lego-60167")]
    public void RepeatedNamesAndTrainBodiesKeepTheirExactPrintedIdentity(string title, string id) =>
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("LEGO Cargo Train")]
    [TestCase("LEGO City")]
    [TestCase("LEGO Technic Volvo")]
    [TestCase("LEGO 60052 Cargo Train 60198")]
    [TestCase("LEGO 42028 Bulldozer 60074")]
    [TestCase("LEGO 10194 Emerald Night Bauanleitung")]
    [TestCase("LEGO 10254 Winter Holiday Train nur OVP")]
    [TestCase("LEGO 10277 Crocodile Locomotive unvollständig")]
    [TestCase("LEGO 60198 Cargo Train bundle")]
    [TestCase("LEGO 42030 kompatibel")]
    [TestCase("LEGO 60051-2")]
    public void NewCoverageDoesNotGuessUnnumberedNamesOrMergeDifferentOfferScopes(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
