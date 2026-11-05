using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoStarWarsInstructionCatalogTests
{
    private const string Source = "seed:lego-official-star-wars-instructions-2026-11-05";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void SupplementContainsOnlyThe139ReviewedPrintedSets() =>
        Assert.That(Catalog.Count(product => product.Source == Source), Is.EqualTo(139));

    [TestCase("75043", "AT-AP™")]
    [TestCase("75048", "The Phantom")]
    [TestCase("75049", "Snowspeeder™")]
    [TestCase("75050", "B-Wing™")]
    [TestCase("75051", "Jedi™ Scout Fighter")]
    [TestCase("75052", "Mos Eisley Cantina™")]
    [TestCase("75053", "The Ghost")]
    [TestCase("75054", "AT-AT™")]
    [TestCase("75055", "Imperial Star Destroyer™")]
    [TestCase("75056", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75058", "MTT™")]
    [TestCase("75059", "Sandcrawler™")]
    [TestCase("75060", "Slave I™")]
    [TestCase("75072", "ARC-170 Starfighter™")]
    [TestCase("75073", "Vulture Droid™")]
    [TestCase("75076", "Republic Gunship™")]
    [TestCase("75077", "Homing Spider Droid™")]
    [TestCase("75078", "Imperial Troop Transport")]
    [TestCase("75079", "Shadow Troopers")]
    [TestCase("75080", "AAT™")]
    [TestCase("75081", "T-16 Skyhopper™")]
    [TestCase("75082", "TIE Advanced Prototype™")]
    [TestCase("75083", "AT-DP™")]
    [TestCase("75084", "Wookiee™ Gunship")]
    [TestCase("75085", "Hailfire Droid™")]
    [TestCase("75086", "Battle Droid™ Troop Carrier")]
    [TestCase("75087", "Anakin's Custom Jedi Starfighter™")]
    [TestCase("75088", "Senate Commando Troopers™")]
    [TestCase("75089", "Geonosis Troopers™")]
    [TestCase("75091", "Flash Speeder™")]
    [TestCase("75092", "Naboo Starfighter™")]
    [TestCase("75093", "Death Star™ Final Duel")]
    [TestCase("75094", "Imperial Shuttle Tydirium™")]
    [TestCase("75095", "TIE Fighter™")]
    [TestCase("75096", "Sith Infiltrator™")]
    [TestCase("75097", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75098", "Assault on Hoth™")]
    [TestCase("75099", "Rey's Speeder™")]
    [TestCase("75100", "First Order Snowspeeder™")]
    [TestCase("75101", "First Order Special Forces TIE fighter™")]
    [TestCase("75102", "Poe's X-Wing Fighter™")]
    [TestCase("75103", "First Order Transporter™")]
    [TestCase("75104", "Kylo Ren's Command Shuttle™")]
    [TestCase("75105", "Millennium Falcon™")]
    [TestCase("75106", "Imperial Assault Carrier™")]
    [TestCase("75107", "Jango Fett™")]
    [TestCase("75108", "Clone Commander Cody™")]
    [TestCase("75109", "Obi-Wan Kenobi™")]
    [TestCase("75110", "Luke Skywalker™")]
    [TestCase("75111", "Darth Vader™")]
    [TestCase("75112", "General Grievous™")]
    [TestCase("75113", "Rey")]
    [TestCase("75114", "First Order Stormtrooper™")]
    [TestCase("75115", "Poe Dameron™")]
    [TestCase("75116", "Finn")]
    [TestCase("75117", "Kylo Ren™")]
    [TestCase("75118", "Captain Phasma™")]
    [TestCase("75119", "Sergeant Jyn Erso™")]
    [TestCase("75120", "K-2SO™")]
    [TestCase("75121", "Imperial Death Trooper™")]
    [TestCase("75125", "Resistance X-Wing Fighter™")]
    [TestCase("75126", "First Order Snowspeeder™")]
    [TestCase("75127", "The Ghost™")]
    [TestCase("75128", "TIE Advanced Prototype™")]
    [TestCase("75129", "Wookiee™ Gunship")]
    [TestCase("75130", "AT-DP™")]
    [TestCase("75131", "Resistance Trooper Battle Pack")]
    [TestCase("75132", "First Order Battle Pack")]
    [TestCase("75133", "Rebel Alliance Battle Pack")]
    [TestCase("75134", "Galactic Empire™ Battle Pack")]
    [TestCase("75135", "Obi-Wan's Jedi Interceptor™")]
    [TestCase("75136", "Droid™ Escape Pod")]
    [TestCase("75137", "Carbon-Freezing Chamber")]
    [TestCase("75139", "Battle on Takodana™")]
    [TestCase("75140", "Resistance Troop Transporter")]
    [TestCase("75141", "Kanan's Speeder Bike™")]
    [TestCase("75142", "Homing Spider Droid™")]
    [TestCase("75144", "Snowspeeder™")]
    [TestCase("75145", "Eclipse Fighter™")]
    [TestCase("75147", "StarScavenger™")]
    [TestCase("75148", "Encounter on Jakku™")]
    [TestCase("75149", "Resistance X-Wing Fighter™")]
    [TestCase("75150", "Vader's TIE Advanced vs. A-Wing Starfighter")]
    [TestCase("75151", "Clone Turbo Tank™")]
    [TestCase("75152", "Imperial Assault Hovertank™")]
    [TestCase("75153", "AT-ST™ Walker")]
    [TestCase("75154", "TIE Striker™")]
    [TestCase("75155", "Rebel U-Wing Fighter™")]
    [TestCase("75156", "Krennic's Imperial Shuttle")]
    [TestCase("75157", "Captain Rex's AT-TE™")]
    [TestCase("75158", "Rebel Combat Frigate")]
    [TestCase("75159", "Death Star™")]
    [TestCase("75162", "Y-Wing™ Microfighter")]
    [TestCase("75164", "Rebel Trooper Battle Pack")]
    [TestCase("75165", "Imperial Trooper Battle Pack")]
    [TestCase("75166", "First Order Transport Speeder Battle Pack")]
    [TestCase("75168", "Yoda's Jedi Starfighter™")]
    [TestCase("75169", "Duel on Naboo™")]
    [TestCase("75170", "The Phantom")]
    [TestCase("75171", "Battle on Scarif")]
    [TestCase("75172", "Y-Wing Starfighter™")]
    [TestCase("75173", "Luke's Landspeeder™")]
    [TestCase("75174", "Desert Skiff Escape")]
    [TestCase("75175", "A-Wing Starfighter™")]
    [TestCase("75176", "Resistance Transport Pod™")]
    [TestCase("75177", "First Order Heavy Scout Walker™")]
    [TestCase("75178", "Jakku Quadjumper™")]
    [TestCase("75179", "Kylo Ren's TIE Fighter™")]
    [TestCase("75180", "Rathtar™ Escape")]
    [TestCase("75181", "Y-Wing Starfighter™")]
    [TestCase("75182", "Republic Fighter Tank™")]
    [TestCase("75183", "Darth Vader™ Transformation")]
    [TestCase("75185", "Tracker I")]
    [TestCase("75186", "The Arrowhead")]
    [TestCase("75187", "BB-8™")]
    [TestCase("75189", "First Order Heavy Assault Walker™")]
    [TestCase("75190", "First Order Star Destroyer™")]
    [TestCase("75191", "Jedi Starfighter™ With Hyperdrive")]
    [TestCase("75193", "Millennium Falcon™ Microfighter")]
    [TestCase("75194", "First Order TIE Fighter™ Microfighter")]
    [TestCase("75195", "Ski Speeder™ vs. First Order Walker™ Microfighters")]
    [TestCase("75196", "A-Wing™ vs. TIE Silencer™ Microfighters")]
    [TestCase("75197", "First Order Specialists Battle Pack")]
    [TestCase("75198", "Tatooine™ Battle Pack")]
    [TestCase("75199", "General Grievous' Combat Speeder")]
    [TestCase("75200", "Ahch-To Island™ Training")]
    [TestCase("75201", "First Order AT-ST™")]
    [TestCase("75202", "Defense of Crait™")]
    [TestCase("75203", "Hoth™ Medical Chamber")]
    [TestCase("75204", "Sandspeeder™")]
    [TestCase("75205", "Mos Eisley Cantina™")]
    [TestCase("75206", "Jedi™ and Clone Troopers™ Battle Pack")]
    [TestCase("75207", "Imperial Patrol Battle Pack")]
    [TestCase("75208", "Yoda's Hut")]
    [TestCase("75209", "Han Solo's Landspeeder™")]
    [TestCase("75211", "Imperial TIE Fighter™")]
    [TestCase("75212", "Kessel Run Millennium Falcon™")]
    [TestCase("75213", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75214", "Anakin's Jedi Starfighter™")]
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
            {
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
                Assert.That(Matcher.Match(alias.ToLowerInvariant())?.Id, Is.EqualTo(id), alias);
                Assert.That(Matcher.Match(alias.ToUpperInvariant())?.Id, Is.EqualTo(id), alias);
            }
            Assert.That(Matcher.Match($"LEGO Set {number} gebraucht")?.Id, Is.EqualTo(id));
            Assert.That(Matcher.Match($"LEGO {number}", brand: "Playmobil"), Is.Null);
            Assert.That(Matcher.Match(number), Is.Null);
        });
    }

    [Test]
    public void IdenticalHeadingsKeepDifferentPrintedIdentities()
    {
        var matches = new[] { "LEGO 75049 Snowspeeder™", "LEGO 75144 Snowspeeder™",
            "LEGO 75125 Resistance X-Wing Fighter™", "LEGO 75149 Resistance X-Wing Fighter™" }
            .Select(title => Matcher.Match(title)?.Id).ToArray();
        Assert.That(matches, Is.EqualTo(new[] { "lego-75049", "lego-75144", "lego-75125", "lego-75149" }));
    }

    [TestCase("LEGO Jedi Scout Fighter")]
    [TestCase("LEGO 75049 Snowspeeder 75144")]
    [TestCase("LEGO 75125 Resistance X-Wing Fighter 75149")]
    [TestCase("LEGO 75043 AT-AP 75048 The Phantom")]
    [TestCase("LEGO 75049-2 Snowspeeder")]
    [TestCase("LEGO 750490 Snowspeeder")]
    [TestCase("Playmobil 75049 Snowspeeder")]
    public void UnnumberedContradictoryOrDifferentPrintedIdentitiesDoNotSelectTheNewSets(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
