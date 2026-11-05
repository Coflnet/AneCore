using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoStarWarsAdditionalInstructionCatalogTests
{
    private const string Source = "seed:lego-official-star-wars-additional-instructions-2026-11-05";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void SupplementContainsOnlyThe147ReviewedPrintedSets() =>
        Assert.That(Catalog.Count(product => product.Source == Source), Is.EqualTo(147));

    [TestCase("75215", "Cloud-Rider Swoop Bikes™")]
    [TestCase("75216", "Snoke's Throne Room")]
    [TestCase("75217", "Imperial Conveyex Transport™")]
    [TestCase("75218", "X-Wing Starfighter™")]
    [TestCase("75219", "Imperial AT-Hauler™")]
    [TestCase("75220", "Sandcrawler™")]
    [TestCase("75221", "Imperial Landing Craft")]
    [TestCase("75222", "Betrayal at Cloud City™")]
    [TestCase("75223", "Naboo Starfighter™ Microfighter")]
    [TestCase("75224", "Sith Infiltrator™ Microfighter")]
    [TestCase("75225", "Elite Praetorian Guard™ Battle Pack")]
    [TestCase("75226", "Inferno Squad™ Battle Pack")]
    [TestCase("75227", "Darth Vader™ Bust")]
    [TestCase("75228", "Escape Pod vs. Dewback™ Microfighters")]
    [TestCase("75229", "Death Star™ Escape")]
    [TestCase("75230", "Porg™")]
    [TestCase("75232", "Kylo Ren™ & Sith Trooper™")]
    [TestCase("75233", "Droid Gunship™")]
    [TestCase("75234", "AT-AP™ Walker")]
    [TestCase("75235", "X-Wing Starfighter™ Trench Run")]
    [TestCase("75236", "Duel on Starkiller Base™")]
    [TestCase("75237", "TIE Fighter™ Attack")]
    [TestCase("75238", "Action Battle Endor™ Assault")]
    [TestCase("75239", "Action Battle Hoth™ Generator Attack")]
    [TestCase("75240", "Major Vonreg’s TIE Fighter™")]
    [TestCase("75241", "Action Battle Echo Base™ Defense")]
    [TestCase("75242", "Black Ace TIE Interceptor™")]
    [TestCase("75243", "Slave I™ – 20th Anniversary Edition")]
    [TestCase("75244", "Tantive IV™")]
    [TestCase("75245", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75246", "Death Star Cannon")]
    [TestCase("75247", "Rebel A-Wing Starfighter™")]
    [TestCase("75248", "Resistance A-Wing Starfighter™")]
    [TestCase("75249", "Resistance Y-Wing Starfighter™")]
    [TestCase("75250", "Pasaana Speeder Chase")]
    [TestCase("75251", "Darth Vader's Castle")]
    [TestCase("75252", "Imperial Star Destroyer™")]
    [TestCase("75253", "Droid Commander")]
    [TestCase("75254", "AT-ST™ Raider")]
    [TestCase("75255", "Yoda™")]
    [TestCase("75256", "Kylo Ren’s Shuttle™")]
    [TestCase("75257", "Millennium Falcon™")]
    [TestCase("75258", "Anakin’s Podracer™ – 20th Anniversary Edition")]
    [TestCase("75259", "Snowspeeder™ – 20th Anniversary Edition")]
    [TestCase("75261", "Clone Scout Walker™ – 20th Anniversary Edition")]
    [TestCase("75262", "Imperial Dropship™ – 20th Anniversary Edition")]
    [TestCase("75263", "Resistance Y-wing™ Microfighter")]
    [TestCase("75264", "Kylo Ren's Shuttle™ Microfighter")]
    [TestCase("75265", "T-16 Skyhopper™ vs Bantha™ Microfighters")]
    [TestCase("75266", "Sith Troopers™ Battle Pack")]
    [TestCase("75267", "Mandalorian™ Battle Pack")]
    [TestCase("75268", "Snowspeeder™")]
    [TestCase("75269", "Duel on Mustafar™")]
    [TestCase("75270", "Obi-Wan's Hut")]
    [TestCase("75271", "Luke Skywalker's Landspeeder™")]
    [TestCase("75272", "Sith TIE Fighter™")]
    [TestCase("75273", "Poe Dameron's X-wing Fighter™")]
    [TestCase("75274", "TIE Fighter Pilot™ Helmet")]
    [TestCase("75275", "A-wing Starfighter™")]
    [TestCase("75276", "Stormtrooper™ Helmet")]
    [TestCase("75277", "Boba Fett™ Helmet")]
    [TestCase("75278", "D-O™")]
    [TestCase("75279", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75280", "501st Legion™ Clone Troopers")]
    [TestCase("75281", "Anakin's Jedi™ Interceptor")]
    [TestCase("75283", "Armored Assault Tank (AAT™)")]
    [TestCase("75284", "Knights of Ren™ Transport Ship")]
    [TestCase("75286", "General Grievous's Starfighter™")]
    [TestCase("75288", "AT-AT™")]
    [TestCase("75290", "Mos Eisley Cantina™")]
    [TestCase("75291", "Death Star™ Final Duel")]
    [TestCase("75292", "The Razor Crest™")]
    [TestCase("75293", "Resistance I-TS Transport")]
    [TestCase("75294", "Bespin™ Duel")]
    [TestCase("75295", "Millennium Falcon™ Microfighter")]
    [TestCase("75296", "Darth Vader™ Meditation Chamber")]
    [TestCase("75297", "Resistance X-Wing™")]
    [TestCase("75298", "AT-AT™ vs. Tauntaun™ Microfighters")]
    [TestCase("75299", "Trouble on Tatooine™")]
    [TestCase("75300", "Imperial TIE Fighter™")]
    [TestCase("75301", "Luke Skywalker's X-Wing Fighter™")]
    [TestCase("75302", "Imperial Shuttle™")]
    [TestCase("75306", "Imperial Probe Droid™")]
    [TestCase("75307", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75308", "R2-D2™")]
    [TestCase("75309", "Republic Gunship™")]
    [TestCase("75310", "Duel on Mandalore™")]
    [TestCase("75311", "Imperial Armored Marauder")]
    [TestCase("75312", "Boba Fett’s Starship™")]
    [TestCase("75313", "AT-AT™")]
    [TestCase("75314", "The Bad Batch™ Attack Shuttle")]
    [TestCase("75315", "Imperial Light Cruiser™")]
    [TestCase("75316", "Mandalorian Starfighter™")]
    [TestCase("75317", "The Mandalorian™ & the Child")]
    [TestCase("75318", "The Child")]
    [TestCase("75319", "The Armorer’s Mandalorian™ Forge")]
    [TestCase("75320", "Snowtrooper™ Battle Pack")]
    [TestCase("75321", "The Razor Crest™ Microfighter")]
    [TestCase("75322", "Hoth™ AT-ST™")]
    [TestCase("75323", "The Justifier™")]
    [TestCase("75324", "Dark Trooper™ Attack")]
    [TestCase("75326", "Boba Fett's Throne Room")]
    [TestCase("75327", "Luke Skywalker™ (Red Five) Helmet")]
    [TestCase("75329", "Death Star™ Trench Run Diorama")]
    [TestCase("75330", "Dagobah™ Jedi™ Training Diorama")]
    [TestCase("75331", "The Razor Crest™")]
    [TestCase("75332", "AT-ST™")]
    [TestCase("75334", "Obi-Wan Kenobi™ vs. Darth Vader™")]
    [TestCase("75335", "BD-1™")]
    [TestCase("75336", "Inquisitor Transport Scythe™")]
    [TestCase("75338", "Ambush on Ferrix™")]
    [TestCase("75339", "Death Star™ Trash Compactor Diorama")]
    [TestCase("75340", "LEGO® Star Wars™ Advent Calendar")]
    [TestCase("75341", "Luke Skywalker’s Landspeeder™")]
    [TestCase("75342", "Republic Fighter Tank™")]
    [TestCase("75343", "Dark Trooper™ Helmet")]
    [TestCase("75344", "Boba Fett's Starship™ Microfighter")]
    [TestCase("75345", "501st Clone Troopers™ Battle Pack")]
    [TestCase("75348", "Mandalorian Fang Fighter vs. TIE Interceptor™")]
    [TestCase("75349", "Captain Rex™ Helmet")]
    [TestCase("75350", "Clone Commander Cody™ Helmet")]
    [TestCase("75351", "Princess Leia™ (Boushh™) Helmet")]
    [TestCase("75352", "Emperor's Throne Room™ Diorama")]
    [TestCase("75354", "Coruscant Guard Gunship™")]
    [TestCase("75355", "X-Wing Starfighter™")]
    [TestCase("75358", "Tenoo Jedi Temple™")]
    [TestCase("75359", "332nd Ahsoka's Clone Trooper™ Battle Pack")]
    [TestCase("75360", "Yoda's Jedi Starfighter™")]
    [TestCase("75361", "Spider Tank")]
    [TestCase("75363", "The Mandalorian N-1 Starfighter™ Microfighter")]
    [TestCase("75364", "New Republic E-Wing™ vs. Shin Hati’s Starfighter™")]
    [TestCase("75365", "Yavin 4 Rebel Base")]
    [TestCase("75366", "Advent Calendar 2023")]
    [TestCase("75368", "Darth Vader™ Mech")]
    [TestCase("75370", "Stormtrooper™ Mech")]
    [TestCase("75371", "Chewbacca™")]
    [TestCase("75376", "Tantive IV™")]
    [TestCase("75378", "BARC Speeder™ Escape")]
    [TestCase("75381", "Droideka™")]
    [TestCase("75384", "The Crimson Firehawk™")]
    [TestCase("75388", "Jedi Bob's Starfighter")]
    [TestCase("75390", "Luke Skywalker™ X-Wing™ Mech")]
    [TestCase("75395", "Advent Calendar 2024")]
    [TestCase("75396", "Desert Skiff & Sarlacc Pit")]
    [TestCase("75401", "Ahsoka's Jedi Interceptor™")]
    [TestCase("75404", "Acclamator-Class Assault Ship™")]
    [TestCase("75405", "Home One Starcruiser")]
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
    public void IdenticalHeadingsKeepNewAndExistingPrintedIdentitiesSeparate()
    {
        var matches = new[] { "LEGO 75055 Imperial Star Destroyer™", "LEGO 75252 Imperial Star Destroyer™",
            "LEGO 75292 The Razor Crest™", "LEGO 75331 The Razor Crest™",
            "LEGO 75288 AT-AT™", "LEGO 75313 AT-AT™" }
            .Select(title => Matcher.Match(title)?.Id).ToArray();
        Assert.That(matches, Is.EqualTo(new[] { "lego-75055", "lego-75252", "lego-75292", "lego-75331", "lego-75288", "lego-75313" }));
    }

    [TestCase("LEGO Cloud-Rider Swoop Bikes")]
    [TestCase("LEGO 75292 The Razor Crest 75331")]
    [TestCase("LEGO 75288 AT-AT 75313")]
    [TestCase("LEGO 75055 Imperial Star Destroyer 75252")]
    [TestCase("LEGO 75292-2 The Razor Crest")]
    [TestCase("LEGO 752920 The Razor Crest")]
    [TestCase("Playmobil 75292 The Razor Crest")]
    public void UnnumberedContradictoryOrDifferentPrintedIdentitiesDoNotSelectTheNewSets(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
