using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoTechnicAdditionalInstructionCatalogTests
{
    private const string Source = "seed:lego-official-technic-additional-instructions-2026-11-05";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void SupplementContainsOnlyThe13ReviewedPrintedSets() =>
        Assert.That(Catalog.Count(product => product.Source == Source), Is.EqualTo(13));

    [TestCase("8043", "Motorized Excavator")]
    [TestCase("8047", "Compact Excavator")]
    [TestCase("8069", "Backhoe Loader")]
    [TestCase("8070", "Supercar")]
    [TestCase("8109", "Flatbed Truck")]
    [TestCase("8258", "Crane Truck")]
    [TestCase("8264", "Hauler")]
    [TestCase("8284", "Dune Buggy")]
    [TestCase("9395", "Pick-up Tow Truck")]
    [TestCase("9397", "Logging Truck")]
    [TestCase("9398", "4X4 Crawler")]
    [TestCase("42041", "Race Truck")]
    [TestCase("42048", "Race Kart")]
    public void OfficialNumberedAliasesResolveWithoutAddingOfferMetadata(string number, string name)
    {
        var id = "lego-" + number;
        var row = Catalog.Single(product => product.Id == id);
        Assert.Multiple(() =>
        {
            Assert.That(Catalog.Count(product => product.Vertical == "lego" && product.Model == number), Is.EqualTo(1));
            Assert.That(row.Source, Is.EqualTo(Source));
            Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "set_number" }));
            Assert.That(row.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { number }));
            Assert.That(row.Aliases, Is.EquivalentTo(new[] { "LEGO " + number, "LEGO Set " + number, "LEGO " + number + " " + name }));
            foreach (var alias in row.Aliases)
            {
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
                Assert.That(Matcher.Match(alias.ToLowerInvariant())?.Id, Is.EqualTo(id), alias);
            }
            Assert.That(Matcher.Match($"LEGO Set {number} gebraucht vollständig")?.Id, Is.EqualTo(id));
            Assert.That(Matcher.Match(number), Is.Null);
            Assert.That(Matcher.Match($"LEGO {number}", brand: "Playmobil"), Is.Null);
            foreach (var suffix in new[] { "Bauanleitung", "nur OVP", "unvollständig", "kompatibel", "bundle" })
                Assert.That(Matcher.Match($"LEGO {number} {suffix}"), Is.Null, suffix);
        });
    }

    [TestCase("LEGO Technic Flatbed Truck")]
    [TestCase("LEGO 8043 Motorized Excavator 8047")]
    [TestCase("LEGO 9395 Pick-up Tow Truck 8109")]
    [TestCase("LEGO 42041-2 Race Truck")]
    [TestCase("LEGO 420410 Race Truck")]
    public void UnnumberedAmbiguousOrDifferentPrintedIdentitiesDoNotSelectTheNewSets(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
