using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoCreatorInstructionCatalogTests
{
    private const string Source = "seed:lego-official-creator-instructions-2026-11-05";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void SupplementContainsOnlyTheSixtyReviewedPrintedSets() =>
        Assert.That(Catalog.Count(product => product.Source == Source), Is.EqualTo(60));

    [TestCase("31023", "Yellow Racers")]
    [TestCase("31024", "Roaring Power")]
    [TestCase("31026", "Bike Shop & Café")]
    [TestCase("31027", "Blue Racer")]
    [TestCase("31028", "Sea Plane")]
    [TestCase("31029", "Cargo Heli")]
    [TestCase("31031", "Rainforest Animals")]
    [TestCase("31032", "Red Creatures")]
    [TestCase("31033", "Vehicle Transporter")]
    [TestCase("31034", "Future flyers")]
    [TestCase("31035", "Beach Hut")]
    [TestCase("31036", "Toy & Grocery Shop")]
    [TestCase("31037", "Adventure Vehicles")]
    [TestCase("31038", "Changing Seasons")]
    [TestCase("31039", "Blue Power Jet")]
    [TestCase("31040", "Desert Racers")]
    [TestCase("31041", "Construction Vehicles")]
    [TestCase("31042", "Super Soarer")]
    [TestCase("31044", "Park Animals")]
    [TestCase("31045", "Ocean Explorer")]
    [TestCase("31046", "Fast Car")]
    [TestCase("31047", "Propeller Plane")]
    [TestCase("31048", "Lakeside Lodge")]
    [TestCase("31049", "Twin Spin Helicopter")]
    [TestCase("31050", "Corner Deli")]
    [TestCase("31051", "Lighthouse Point")]
    [TestCase("31052", "Vacation Getaways")]
    [TestCase("31054", "Blue Express")]
    [TestCase("31055", "Red racer")]
    [TestCase("31057", "Air Blazer")]
    [TestCase("31059", "Sunset Street Bike")]
    [TestCase("31060", "Airshow Aces")]
    [TestCase("31063", "Beachside Vacation")]
    [TestCase("31064", "Island Adventures")]
    [TestCase("31066", "Space Shuttle Explorer")]
    [TestCase("31068", "Modular Modern Home")]
    [TestCase("31069", "Modular Family Villa")]
    [TestCase("31070", "Turbo Track Racer")]
    [TestCase("31071", "Drone Explorer")]
    [TestCase("31072", "Extreme Engines")]
    [TestCase("31073", "Mythical Creatures")]
    [TestCase("31074", "Rocket Rally Car")]
    [TestCase("31075", "Outback Adventures")]
    [TestCase("31076", "Daredevil Stunt Plane")]
    [TestCase("31077", "Modular Sweet Surprises")]
    [TestCase("31078", "Treehouse Treasures")]
    [TestCase("31080", "Modular Winter Vacation")]
    [TestCase("31081", "Modular Skate House")]
    [TestCase("31083", "Cruising Adventures")]
    [TestCase("31087", "Dune Buggy")]
    [TestCase("31088", "Deep Sea Creatures")]
    [TestCase("31089", "Sunset Track Racer")]
    [TestCase("31090", "Underwater Robot")]
    [TestCase("31091", "Shuttle Transporter")]
    [TestCase("31092", "Helicopter Adventure")]
    [TestCase("31095", "Fairground Carousel")]
    [TestCase("31096", "Twin-Rotor Helicopter")]
    [TestCase("31097", "Townhouse Pet Shop & Café")]
    [TestCase("31098", "Outback Cabin")]
    [TestCase("31099", "Propeller Plane")]
    public void ExactOfficialIdentityLoadsOnlyVerifiedAttributesAndResolvesEachAlias(string number, string officialName)
    {
        var id = "lego-" + number;
        var row = Catalog.SingleOrDefault(product => product.Id == id);
        Assert.That(row, Is.Not.Null, id);
        Assert.Multiple(() =>
        {
            Assert.That(Catalog.Count(product => product.Id == id || product.Vertical == "lego" && product.Model == number), Is.EqualTo(1), id);
            Assert.That(row!.Brand, Is.EqualTo("LEGO"));
            Assert.That(row.Model, Is.EqualTo(number));
            Assert.That(row.Name, Is.EqualTo("LEGO " + number));
            Assert.That(row.Vertical, Is.EqualTo("lego"));
            Assert.That(row.Source, Is.EqualTo(Source));
            Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "set_number" }));
            Assert.That(row.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { number }));
            Assert.That(row.Aliases, Is.EquivalentTo(new[] { "LEGO " + number, "LEGO Set " + number, "LEGO " + number + " " + officialName }));
            foreach (var alias in row.Aliases)
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
            Assert.That(Matcher.Match($"LEGO Set {number} gebraucht")?.Id, Is.EqualTo(id));
            Assert.That(Matcher.Match($"LEGO {number}", brand: "Playmobil"), Is.Null);
            Assert.That(Matcher.Match(number), Is.Null);
        });
    }

    [Test]
    public void IdenticalPropellerPlaneHeadingsKeepDifferentPrintedIdentities()
    {
        var matches = new[] { "LEGO 31047 Propeller Plane", "LEGO 31099 Propeller Plane" }
            .Select(title => Matcher.Match(title)?.Id).ToArray();
        Assert.That(matches, Is.EqualTo(new[] { "lego-31047", "lego-31099" }));
    }

    [TestCase("LEGO Propeller Plane")]
    [TestCase("LEGO 31047 Propeller Plane 31099")]
    [TestCase("LEGO 31023 Yellow Racers 31024 Roaring Power")]
    [TestCase("LEGO 31047-2 Propeller Plane")]
    [TestCase("LEGO 310470 Propeller Plane")]
    [TestCase("Playmobil 31047 Propeller Plane")]
    public void UnnumberedContradictoryOrDifferentPrintedIdentitiesDoNotSelectTheNewSets(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
