using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class LegoFriendsRetiredCatalogTests
{
    private const string Source = "seed:lego-official-friends-retired-2026-10-29-r48";
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [Test]
    public void SupplementContainsOnlyTheReviewedDistinctPrintedSets() =>
        Assert.That(Catalog.Count(p => p.Source == Source), Is.EqualTo(141));

    [TestCase("41300", "Puppy Championship")]
    [TestCase("41301", "Puppy Parade")]
    [TestCase("41302", "Puppy Pampering")]
    [TestCase("41303", "Puppy Playground")]
    [TestCase("41304", "Puppy Treats & Tricks")]
    [TestCase("41305", "Emma's Photo Studio")]
    [TestCase("41306", "Mia's Beach Scooter")]
    [TestCase("41307", "Olivia's Creative Lab")]
    [TestCase("41308", "Stephanie's Friendship Cakes")]
    [TestCase("41309", "Andrea's Musical Duet")]
    [TestCase("41310", "Heartlake Gift Delivery")]
    [TestCase("41311", "Heartlake Pizzeria")]
    [TestCase("41312", "Heartlake Sports Center")]
    [TestCase("41313", "Heartlake Summer Pool")]
    [TestCase("41314", "Stephanie's House")]
    [TestCase("41315", "Heartlake Surf Shop")]
    [TestCase("41316", "Andrea's Speedboat Transporter")]
    [TestCase("41317", "Sunshine Catamaran")]
    [TestCase("41318", "Heartlake Hospital")]
    [TestCase("41319", "Snow Resort Hot Chocolate Van")]
    [TestCase("41320", "Heartlake Frozen Yogurt Shop")]
    [TestCase("41321", "Snow Resort Off-Roader")]
    [TestCase("41322", "Snow Resort Ice Rink")]
    [TestCase("41323", "Snow Resort Chalet")]
    [TestCase("41324", "Snow Resort Ski Lift")]
    [TestCase("41325", "Heartlake City Playground")]
    [TestCase("41326", "LEGO® Friends Advent Calendar")]
    [TestCase("41327", "Mia's Bedroom")]
    [TestCase("41328", "Stephanie's Bedroom")]
    [TestCase("41329", "Olivia's Deluxe Bedroom")]
    [TestCase("41330", "Stephanie's Soccer Practice")]
    [TestCase("41332", "Emma's Art Stand")]
    [TestCase("41333", "Olivia's Mission Vehicle")]
    [TestCase("41334", "Andrea's Park Performance")]
    [TestCase("41335", "Mia's Tree House")]
    [TestCase("41336", "Emma's Art Café")]
    [TestCase("41337", "Underwater Loop")]
    [TestCase("41338", "Stephanie's Sports Arena")]
    [TestCase("41339", "Mia's Camper Van")]
    [TestCase("41340", "Friendship House")]
    [TestCase("41341", "Andrea's Bedroom")]
    [TestCase("41342", "Emma's Deluxe Bedroom")]
    [TestCase("41343", "Heartlake City Airplane Tour")]
    [TestCase("41344", "Andrea's Accessories Store")]
    [TestCase("41345", "Heartlake City Pet Center")]
    [TestCase("41346", "Friendship Box")]
    [TestCase("41347", "Heartlake City Resort")]
    [TestCase("41348", "Service & Care Truck")]
    [TestCase("41349", "Drifting Diner")]
    [TestCase("41350", "Spinning Brushes Car Wash")]
    [TestCase("41351", "Creative Tuning Shop")]
    [TestCase("41352", "The Big Race Day")]
    [TestCase("41353", "LEGO® Friends Advent Calendar")]
    [TestCase("41354", "Andrea's Heart Box")]
    [TestCase("41355", "Emma's Heart Box")]
    [TestCase("41356", "Stephanie's Heart Box")]
    [TestCase("41357", "Olivia's Heart Box")]
    [TestCase("41358", "Mia's Heart Box")]
    [TestCase("41359", "Heart Box Friendship Pack")]
    [TestCase("41360", "Emma's Mobile Vet Clinic")]
    [TestCase("41361", "Mia's Foal Stable")]
    [TestCase("41362", "Heartlake City Supermarket")]
    [TestCase("41363", "Mia's Forest Adventure")]
    [TestCase("41364", "Stephanie's Buggy & Trailer")]
    [TestCase("41365", "Emma's Art Studio")]
    [TestCase("41366", "Olivia's Cupcake Café")]
    [TestCase("41367", "Stephanie's Horse Jumping")]
    [TestCase("41368", "Andrea's Talent Show")]
    [TestCase("41369", "Mia's House")]
    [TestCase("41371", "Mia's Horse Trailer")]
    [TestCase("41372", "Stephanie's Gymnastics Show")]
    [TestCase("41373", "Funny Octopus Ride")]
    [TestCase("41374", "Andrea's Pool Party")]
    [TestCase("41375", "Heartlake City Amusement Pier")]
    [TestCase("41376", "Turtles Rescue Mission")]
    [TestCase("41378", "Dolphins Rescue Mission")]
    [TestCase("41379", "Heartlake City Restaurant")]
    [TestCase("41380", "Lighthouse Rescue Center")]
    [TestCase("41381", "Rescue Mission Boat")]
    [TestCase("41382", "LEGO® Friends Advent Calendar")]
    [TestCase("41383", "Olivia's Hamster Playground")]
    [TestCase("41384", "Andrea's Summer Heart Box")]
    [TestCase("41385", "Emma's Summer Heart Box")]
    [TestCase("41386", "Stephanie's Summer Heart Box")]
    [TestCase("41387", "Olivia's Summer Heart Box")]
    [TestCase("41388", "Mia's Summer Heart Box")]
    [TestCase("41389", "Ice Cream Cart")]
    [TestCase("41390", "Andrea's Car & Stage")]
    [TestCase("41391", "Heartlake City Hair Salon")]
    [TestCase("41392", "Nature Glamping")]
    [TestCase("41393", "Baking Competition")]
    [TestCase("41394", "Heartlake City Hospital")]
    [TestCase("41395", "Friendship Bus")]
    [TestCase("41396", "Puppy Playground")]
    [TestCase("41397", "Juice Truck")]
    [TestCase("41398", "4+ Stephanie's House")]
    [TestCase("41400", "Andrea's Play Cube")]
    [TestCase("41401", "Stephanie's Play Cube")]
    [TestCase("41402", "Olivia's Play Cube")]
    [TestCase("41403", "Mia's Play Cube")]
    [TestCase("41404", "Emma's Play Cube")]
    [TestCase("41405", "Andrea's Shopping Play Cube")]
    [TestCase("41406", "Stephanie's Shopping Play Cube")]
    [TestCase("41407", "Olivia's Shopping Play Cube")]
    [TestCase("41408", "Mia's Shopping Play Cube")]
    [TestCase("41409", "Emma's Shopping Play Cube")]
    [TestCase("41410", "Andrea's Summer Play Cube")]
    [TestCase("41411", "Stephanie's Summer Play Cube")]
    [TestCase("41412", "Olivia's Summer Play Cube")]
    [TestCase("41413", "Mia's Summer Play Cube")]
    [TestCase("41414", "Emma's Summer Play Cube")]
    [TestCase("41421", "Baby Elephant Jungle Rescue")]
    [TestCase("41422", "Panda Jungle Tree House")]
    [TestCase("41423", "Tiger Hot Air Balloon Jungle Rescue")]
    [TestCase("41424", "Jungle Rescue Base")]
    [TestCase("41425", "Olivia's Flower Garden")]
    [TestCase("41426", "Heartlake City Park Café")]
    [TestCase("41427", "Emma's Fashion Shop")]
    [TestCase("41428", "Beach House")]
    [TestCase("41429", "Heartlake City Airplane")]
    [TestCase("41430", "Summer Fun Water Park")]
    [TestCase("41431", "Heartlake City Brick Box")]
    [TestCase("41432", "Alpaca Mountain Jungle Rescue")]
    [TestCase("41433", "Party Boat")]
    [TestCase("41434", "Andrea's Jungle Play Cube")]
    [TestCase("41435", "Stephanie's Jungle Play Cube")]
    [TestCase("41436", "Olivia's Jungle Play Cube")]
    [TestCase("41437", "Mia's Jungle Play Cube")]
    [TestCase("41438", "Emma's Jungle Play Cube")]
    [TestCase("41439", "Cat Grooming Car")]
    [TestCase("41440", "Heartlake City Bakery")]
    [TestCase("41441", "Horse Training and Trailer")]
    [TestCase("41442", "Vet Clinic Rescue Buggy")]
    [TestCase("41443", "Olivia's Electric Car")]
    [TestCase("41444", "Heartlake City Organic Café")]
    [TestCase("41445", "Vet Clinic Ambulance")]
    [TestCase("41446", "Heartlake City Vet Clinic")]
    [TestCase("41447", "Heartlake City Park")]
    [TestCase("41448", "Heartlake City Movie Theater")]
    [TestCase("41449", "Andrea's Family House")]
    [TestCase("41450", "Heartlake City Shopping Mall")]
    public void OfficialPrintedIdentityResolvesExactAliasesAndPreservesOfferBoundaries(string number, string officialName)
    {
        var id = "lego-" + number;
        var row = Catalog.SingleOrDefault(p => p.Id == id);
        Assert.That(row, Is.Not.Null, id);
        Assert.Multiple(() =>
        {
            Assert.That(Catalog.Count(p => p.Id == id || p.Vertical == "lego" && p.Model == number), Is.EqualTo(1), id);
            Assert.That(row!.Brand, Is.EqualTo("LEGO"));
            Assert.That(row.Model, Is.EqualTo(number));
            Assert.That(row.Name, Is.EqualTo("LEGO " + number));
            Assert.That(row.Vertical, Is.EqualTo("lego"));
            Assert.That(row.Source, Is.EqualTo(Source));
            Assert.That(row.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "set_number" }));
            Assert.That(row.PossibleAttributes["set_number"], Is.EquivalentTo(new[] { number }));
            Assert.That(row.Aliases, Is.EquivalentTo(new[] { "LEGO " + number, "LEGO Set " + number, "LEGO Friends " + number, "LEGO " + number + " " + officialName }));
            foreach (var alias in row.Aliases)
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(id), alias);
            Assert.That(Matcher.Match($"LEGO Friends {number} gebraucht vollständig")?.Id, Is.EqualTo(id));
            Assert.That(Matcher.Match($"LEGO {number}", brand: "Playmobil"), Is.Null);
            Assert.That(Matcher.Match($"LEGO {number} Bauanleitung"), Is.Null);
            Assert.That(Matcher.Match($"LEGO {number} nur OVP"), Is.Null);
            Assert.That(Matcher.Match($"LEGO {number} Ersatzteile"), Is.Null);
            Assert.That(Matcher.Match($"LEGO {number} unvollständig"), Is.Null);
            Assert.That(Matcher.Match(number), Is.Null);
        });
    }

    [TestCase("LEGO Friends")]
    [TestCase("LEGO Friendship Box")]
    [TestCase("LEGO Heartlake City Brick Box")]
    [TestCase("LEGO 41333 Olivia's Mission Vehicle 41428 Beach House")]
    [TestCase("LEGO 41357 Olivia's Heart Box 41431 Heartlake City Brick Box")]
    [TestCase("LEGO 41333-2")]
    [TestCase("LEGO 413330")]
    [TestCase("Playmobil 41333 Olivia's Mission Vehicle")]
    public void CoverageDoesNotInferUnnumberedNamesOtherBrandsPackagesOrDifferentPrintedNumbers(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);
}
