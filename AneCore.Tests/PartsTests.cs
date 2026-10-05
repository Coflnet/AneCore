using Coflnet.Ane;
using Coflnet.Ane.Parts;

namespace AneCore.Tests;

[TestFixture]
public class PartClassRegistryTests
{
    // B1: the class is decided from the title only, with anchored nouns and an exclusion list
    [TestCase("Metal Gear Solid PS2")]
    [TestCase("Gears of War 3 Xbox 360")]
    [TestCase("Top Gear DVD Box")]
    [TestCase("Samsung Galaxy Gear S3 Frontier Smartwatch")]
    [TestCase("Samsung Gear VR Brille")]
    [TestCase("Gear Fit 2 Pro")]
    [TestCase("Mountainbike 26 Zoll 21 Gang Shimano Schaltwerk")]
    [TestCase("Mountain bike gears shifter derailleur set")]
    [TestCase("iPhone 11 64GB")]
    [TestCase("Canon EOS 5D Kamera")]
    [TestCase("Bosch Winkelschleifer GWS 7-115")]
    [TestCase("Gaming Gear Bundle Headset Maus")]
    [TestCase("Modell Zahnradbahn Spur H0")]
    [TestCase("Getriebe komplett für Bosch GSR")]
    [TestCase("Nikon Z50 Kit")]
    [TestCase("Kawasaki Z125 Pro")]
    [TestCase("Bluetooth Lautsprecher transparent")]
    public void ExcludedContexts_AreNoPart(string title)
    {
        Assert.That(PartClassRegistry.Classify(title), Is.Null, title);
        Assert.That(GearTextExtractor.IsGearText(title), Is.False, title);
        Assert.That(PartTextExtractors.Extract(title, "Zahnrad Bluetooth tooth spare transparent"), Is.Null, "the description never decides");
    }

    [Test]
    public void Description_NeverMakesAPart()
    {
        // the review's false positives: a phone whose description mentions a gear icon, a camera, a grinder
        Assert.That(PartTextExtractors.Extract("Apple iPhone 12 128GB", "Tap the gear icon in settings. Bluetooth works. Spare charger included."), Is.Null);
        Assert.That(PartTextExtractors.Extract("Canon EOS 600D", "Zahnrad des Objektivs läuft sauber"), Is.Null);
        Assert.That(PartTextExtractors.Extract("Bosch GWS 850 Winkelschleifer", "Kegelrad neu, 20 Zähne"), Is.Null);
        Assert.That(PartTextExtractors.Extract("Cube Mountainbike 29 Zoll", "Ritzel 32 Zähne, 1600 EUR VHB, nur für Selbstabholer, paypal"), Is.Null);
    }

    [TestCase("Zahnrad 42 Zähne", "gear")]
    [TestCase("Ritzel 12 Zähne Modul 1", "gear")]
    [TestCase("Replacement gear 36 teeth for KitchenAid mixer", "gear")]
    [TestCase("Engrenage 30 dents module 1", "gear")]
    [TestCase("Ingranaggio 25 denti", "gear")]
    [TestCase("Tandwiel 40 tanden", "gear")]
    [TestCase("Zahnriemenrad 20 Zähne", "gear")]
    [TestCase("Kugellager 6203-2RS", "bearing")]
    [TestCase("Ball bearing 608ZZ skateboard", "bearing")]
    [TestCase("Keilriemen SPZ 1000", "belt")]
    [TestCase("Kettenrad 42 Zähne Simson", "sprocket")]
    [TestCase("Kohlebürsten für Bosch GWS", "brush")]
    [TestCase("Luftfilter Stihl MS 170", "filter")]
    public void Nouns_GiveTheClass(string title, string expected) =>
        Assert.That(PartClassRegistry.Classify(title)?.Class.Name, Is.EqualTo(expected), title);

    [Test]
    public void Registry_HasAPrimaryKeyAndKeysForEveryClass()
    {
        foreach (var cls in PartClassRegistry.All)
        {
            Assert.That(cls.Keys, Does.Contain(cls.PrimaryKey).Or.Contain(PartAttributeKeys.PartNumber), cls.Name);
            Assert.That(PartClasses.IsKnown(cls.Name), Is.True);
        }
        Assert.That(PartClasses.IsKnown("widget"), Is.False);
        Assert.That(PartAttributeKeys.IsIndexKey("gear_teeth"), Is.True);
        Assert.That(PartAttributeKeys.IsIndexKey("part_fits"), Is.True);
        Assert.That(PartAttributeKeys.IsIndexKey("color"), Is.False);
        Assert.That(PartAttributeKeys.Searchable, Has.Count.EqualTo(3), "the search index gains at most three attribute pairs per part product");
    }

    [Test]
    public void GenericPart_NeedsALabelledPartNumber()
    {
        Assert.That(PartTextExtractors.Extract("Ersatzteil für Miele Waschmaschine", "gebraucht"), Is.Null);
        var facts = PartTextExtractors.Extract("Ersatzteil Miele Waschmaschine", "Teile-Nr. 5091740 neu")!;
        Assert.That(facts.Class.Name, Is.EqualTo(PartClasses.Generic));
        Assert.That(PartTextExtractors.PrimaryKey(facts, null, 0)!.Value.Value, Is.EqualTo("5091740"));
    }
}

[TestFixture]
public class GearTextExtractorTests
{
    [Test]
    public void German_SpurGear_ReadsEverything()
    {
        var facts = GearTextExtractor.Extract("Zahnrad Messing 42 Zähne Modul 1 Bohrung 8 mm Außendurchmesser 44 mm für Bosch GSR 12V",
            "Ersatzteil, Breite 10 mm, Teile-Nr. 1 619 P08 924. Abholung oder Versand.");

        Assert.Multiple(() =>
        {
            Assert.That(facts.IsGear, Is.True);
            Assert.That(facts.Teeth!.Value, Is.EqualTo(42));
            Assert.That(facts.Teeth.Confidence, Is.GreaterThanOrEqualTo(0.9));
            Assert.That(facts.Module!.Value, Is.EqualTo(1));
            Assert.That(facts.BoreMm!.Value, Is.EqualTo(8));
            Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(44));
            Assert.That(facts.WidthMm!.Value, Is.EqualTo(10));
            Assert.That(facts.Material!.Value, Is.EqualTo("brass"));
            Assert.That(facts.PartNumbers.Select(p => p.Key), Does.Contain("1619P08924"));
            Assert.That(facts.Fits.Select(f => f.MachineKey), Does.Contain("bosch gsr 12v"));
        });
    }

    [Test]
    public void English_ShortForms_AreRead()
    {
        var facts = GearTextExtractor.Extract("Steel spur gear 24T module 1.5 OD 39mm bore 10mm for Dewalt DCD791", null);

        Assert.That(facts.IsGear, Is.True);
        Assert.That(facts.Teeth!.Value, Is.EqualTo(24));
        Assert.That(facts.Teeth.Confidence, Is.LessThan(0.9), "a bare 24T is less sure than '24 teeth'");
        Assert.That(facts.Module!.Value, Is.EqualTo(1.5));
        Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(39));
        Assert.That(facts.BoreMm!.Value, Is.EqualTo(10));
        Assert.That(facts.Material!.Value, Is.EqualTo("steel"));
        Assert.That(facts.Fits.Select(f => f.MachineKey), Does.Contain("dewalt dcd791"));
    }

    [Test]
    public void ZEquals_AndDiameterGlyph_AndCentimetres()
    {
        var facts = GearTextExtractor.Extract("Stirnrad Z=60 ø 6,2 cm Kunststoff", null);
        Assert.That(facts.Teeth!.Value, Is.EqualTo(60));
        Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(62));
        Assert.That(facts.Material!.Value, Is.EqualTo("plastic"));
    }

    // S2: a count only next to a tooth word; model names are never counts
    [Test]
    public void ToothCount_NeverFromModelNames()
    {
        Assert.That(GearTextExtractor.Extract("Zahnrad für Kawasaki Z125", null).Teeth, Is.Null);
        Assert.That(GearTextExtractor.Extract("Zahnrad passend für Nikon Z50 Objektiv", null).Teeth, Is.Null);
        Assert.That(GearTextExtractor.Extract("Zahnrad Sony Xperia Z5 Reparatur", null).Teeth, Is.Null);
        Assert.That(GearTextExtractor.Extract("Zahnrad Z 30 Messing", null).Teeth!.Value, Is.EqualTo(30), "Z followed by a number in a gear title is a count");
        Assert.That(GearTextExtractor.Extract("Zahnrad 20 T Stahl", null).Teeth!.Value, Is.EqualTo(20), "20 T with a gear noun in the title");
        Assert.That(GearTextExtractor.Extract("Zahnrad 500 T", null).Teeth, Is.Null, "above 400 is no gear");
    }

    // S3: the module only in its explicit forms; M8 is a thread
    [Test]
    public void Module_NotFromThreads()
    {
        Assert.That(GearTextExtractor.Extract("Zahnrad mit M8 Gewinde 20 Zähne", null).Module, Is.Null);
        Assert.That(GearTextExtractor.Extract("Zahnrad M1.5 20 Zähne", null).Module, Is.Null, "M1.5 alone is ambiguous");
        Assert.That(GearTextExtractor.Extract("Zahnrad Modul 1.5", null).Module!.Value, Is.EqualTo(1.5));
        Assert.That(GearTextExtractor.Extract("Spur gear module 2", null).Module!.Value, Is.EqualTo(2));
        Assert.That(GearTextExtractor.Extract("Zahnrad m=1,5", null).Module!.Value, Is.EqualTo(1.5));
        Assert.That(GearTextExtractor.Extract("Zahnrad Mod. 2 Stahl", null).Module!.Value, Is.EqualTo(2));
    }

    // S4: the bore only next to a bore word, every number with its own unit
    [Test]
    public void Bore_OnlyNextToABoreWord_UnitPerNumber()
    {
        Assert.That(GearTextExtractor.Extract("Zahnrad 20 Zähne, Welle 150 mm lang", null).BoreMm, Is.Null);
        Assert.That(GearTextExtractor.Extract("Zahnrad 20 Zähne Achse 6 mm", null).BoreMm, Is.Null);
        var facts = GearTextExtractor.Extract("Zahnrad 20 Zähne Durchmesser 3 cm x 5 mm", null);
        Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(30));
        Assert.That(facts.WidthMm!.Value, Is.EqualTo(5));
        Assert.That(GearTextExtractor.Extract("Zahnrad 20 Zähne Innendurchmesser 6 mm", null).BoreMm!.Value, Is.EqualTo(6));
        Assert.That(GearTextExtractor.Extract("Spur gear 20 teeth bore 6 mm", null).BoreMm!.Value, Is.EqualTo(6));
        Assert.That(GearTextExtractor.Extract("Engrenage 20 dents alésage 6 mm", null).BoreMm!.Value, Is.EqualTo(6));
        Assert.That(GearTextExtractor.Extract("Zahnrad 20 Zähne ø innen 6 mm", null).BoreMm!.Value, Is.EqualTo(6));
    }

    [Test]
    public void BoreWithDiameterGlyph_IsNotTheOuterDiameter()
    {
        var facts = GearTextExtractor.Extract("Zahnrad 30 Zähne Bohrung ø 8 mm", null);
        Assert.That(facts.BoreMm!.Value, Is.EqualTo(8));
        Assert.That(facts.OuterDiameterMm, Is.Null);
    }

    [Test]
    public void SeveralToothCounts_GiveNoCountAndANote()
    {
        var facts = GearTextExtractor.Extract("Doppelzahnrad 42/12 Zähne Modul 0,5", null);
        Assert.That(facts.IsGear, Is.True);
        Assert.That(facts.Teeth, Is.Null);
        Assert.That(facts.TeethNote, Does.Contain("several tooth counts"));

        facts = GearTextExtractor.Extract("Zahnradsatz 20 Zähne und 40 Zähne", null);
        Assert.That(facts.Teeth, Is.Null);
        Assert.That(facts.TeethNote, Does.Contain("several"));
    }

    [Test]
    public void NoToothCount_StaysUnknown_NeverGuessed()
    {
        var facts = GearTextExtractor.Extract("Zahnrad für Bohrmaschine, gebraucht", null);
        Assert.That(facts.IsGear, Is.True);
        Assert.That(facts.Teeth, Is.Null);
        Assert.That(facts.TeethNote, Is.Null);
        Assert.That(facts.OuterDiameterMm, Is.Null);
    }

    [Test]
    public void BoreLargerThanOuter_DropsBoth()
    {
        var facts = GearTextExtractor.Extract("Zahnrad 20 Zähne Bohrung 50 mm Durchmesser 20 mm", null);
        Assert.That(facts.BoreMm, Is.Null);
        Assert.That(facts.OuterDiameterMm, Is.Null);
        Assert.That(facts.Teeth!.Value, Is.EqualTo(20));
    }

    [Test]
    public void ConflictingMaterials_GiveNone()
    {
        var facts = GearTextExtractor.Extract("Zahnrad Messing oder Stahl 20 Zähne", null);
        Assert.That(facts.Material, Is.Null);
    }

    [Test]
    public void Evidence_HasNoContactData()
    {
        var facts = GearTextExtractor.Extract("Zahnrad 42 Zähne für Makita HR2470", "Teile-Nr. 1619P08924. Tel 0151 234 56789 oder mail@example.com");
        var all = string.Join(" ", new[] { facts.GearEvidence, facts.Teeth?.Evidence }.Concat(facts.PartNumbers.Select(p => p.Evidence)).Concat(facts.Fits.Select(f => f.Evidence)));
        Assert.That(all, Does.Not.Contain("example.com"));
        Assert.That(all, Does.Not.Contain("56789"));
        Assert.That(facts.PartNumbers.Select(p => p.Key), Does.Contain("1619P08924"));
    }
}

[TestFixture]
public class BearingTextExtractorTests
{
    [Test]
    public void Designation_IsTheKey_DimensionsFromTextOrTable()
    {
        var facts = PartTextExtractors.Extract("Kugellager 6203-2RS", "2 Stück, 17x40x12 mm, neu")!;
        Assert.That(facts.Class.Name, Is.EqualTo(PartClasses.Bearing));
        Assert.That(facts.Designation!.Value, Is.EqualTo("6203-2RS"));
        Assert.That(facts.InnerDiameterMm!.Value, Is.EqualTo(17));
        Assert.That(facts.OuterDiameterMm!.Value, Is.EqualTo(40));
        Assert.That(facts.WidthMm!.Value, Is.EqualTo(12));
        Assert.That(PartTextExtractors.PrimaryKey(facts, null, 0)!.Value.Value, Is.EqualTo("6203-2RS"));

        var table = PartTextExtractors.Extract("Rillenkugellager 608 ZZ Skateboard", null)!;
        Assert.That(table.Designation!.Value, Is.EqualTo("608-2Z"), "ZZ and 2Z are one suffix");
        Assert.That(table.InnerDiameterMm!.Value, Is.EqualTo(8));
        Assert.That(table.OuterDiameterMm!.Value, Is.EqualTo(22));
        Assert.That(table.InnerDiameterMm.Confidence, Is.LessThan(facts.InnerDiameterMm.Confidence), "a table value is less sure than a stated one");
    }

    [Test]
    public void NoDesignation_IsUnknown()
    {
        var facts = PartTextExtractors.Extract("Kugellager Satz gebraucht", "diverse Größen")!;
        Assert.That(facts.Designation, Is.Null);
        Assert.That(PartTextExtractors.PrimaryKey(facts, null, 0), Is.Null);
    }
}

[TestFixture]
public class PartNumbersTests
{
    [Test]
    public void Labelled_HasHighConfidence()
    {
        var found = PartNumbers.Find("Ersatzteil Art.-Nr. 2609110334 passend");
        Assert.That(found, Has.Count.EqualTo(1));
        Assert.That(found[0].Key, Is.EqualTo("2609110334"));
        Assert.That(found[0].Confidence, Is.EqualTo(0.95));
        Assert.That(found[0].Labelled, Is.True);
    }

    [Test]
    public void BoschSpacedFormat_IsOneKey()
    {
        var found = PartNumbers.Find("Zahnrad 1 619 P08 924 neu");
        Assert.That(found.Select(f => f.Key), Is.EqualTo(new[] { "1619P08924" }));
        Assert.That(PartNumbers.Key("1619p08924"), Is.EqualTo("1619P08924"));
    }

    // B2: a labelled value ends at the first prose word
    [Test]
    public void Labelled_StopsBeforeProse()
    {
        var found = PartNumbers.Find("Teile-Nr. 1619P08924 neu und ovp");
        Assert.That(found.Select(f => f.Value), Is.EqualTo(new[] { "1619P08924" }));
        Assert.That(PartNumbers.Find("Refurbished 2023 a"), Is.Empty, "'Ref' inside Refurbished is no label");
        Assert.That(PartNumbers.Find("Part No: ABC-1234-X und Rechnung"), Has.Exactly(1).Items.With.Property("Key").EqualTo("ABC1234X"));
    }

    // S1: contact data, prices, years and capacities are never part numbers
    [Test]
    public void ContactData_Prices_Capacities_AreRejected()
    {
        Assert.That(PartNumbers.Find("Kontakt: 1621234567"), Is.Empty);
        Assert.That(PartNumbers.Find("Tel. 0151 2345678, Zahnrad"), Is.Empty);
        Assert.That(PartNumbers.Find("WhatsApp hans12345 melden"), Is.Empty);
        Assert.That(PartNumbers.Find("Zahnrad hans12345 gebraucht"), Is.Empty, "a lower case user name is no code");
        Assert.That(PartNumbers.Find("Zahnrad 1621234567 gebraucht"), Is.Empty, "a bare digit run is a phone number");
        Assert.That(PartNumbers.Find("Akku 5000mAh 64GB 2000W 230V 2023"), Is.Empty);
        Assert.That(PartNumbers.Find("Preis 1234,50 EUR, Art.-Nr. 12345 EUR"), Is.Empty, "a number followed by a currency is a price");
        Assert.That(PartNumbers.Find("Ref: 1600 EUR VHB"), Is.Empty);
    }

    [Test]
    public void BareCode_NeedsLettersAndFiveDigits()
    {
        Assert.That(PartNumbers.Find("Zahnrad GSR 12V Bosch").Select(f => f.Key), Is.Empty, "a machine model is no part number");
        Assert.That(PartNumbers.Find("Zahnrad 42T M1.5 35mm 18V 230V").Select(f => f.Key), Is.Empty, "measures are no part numbers");
        Assert.That(PartNumbers.Find("Zahnrad 1600A00F7V").Select(f => f.Key), Is.EqualTo(new[] { "1600A00F7V" }));
        Assert.That(PartNumbers.Find("Zahnrad ABC-12345-X").Select(f => f.Key), Is.EqualTo(new[] { "ABC12345X" }));
        Assert.That(PartNumbers.Find("Zahnrad 1600A00F7V")[0].Labelled, Is.False);
    }

    [Test]
    public void PureDigitRuns_AreNotRead()
    {
        Assert.That(PartNumbers.Find("Zahnrad 01512345678 anrufen"), Is.Empty);
    }
}

[TestFixture]
public class FitmentTests
{
    [Test]
    public void German_Phrases()
    {
        var fits = Fitment.Find("Zahnrad passend für Bosch GSR 12V-15 und Makita DHP 453, 42 Zähne", 0.75);
        Assert.That(fits.Select(f => f.MachineKey), Is.EqualTo(new[] { "bosch gsr 12v 15", "makita dhp 453" }));
        Assert.That(fits[0].MachineName, Is.EqualTo("Bosch GSR 12V-15"), "the name keeps the seller's casing");
    }

    [Test]
    public void StopsAtMeasures_AndIgnoresPartWords()
    {
        Assert.That(Fitment.Find("Ritzel für Zahnrad 20 Zähne", 0.75), Is.Empty);
        Assert.That(Fitment.Find("gear for KitchenAid 5KSM150 36 teeth", 0.75).Select(f => f.MachineKey), Is.EqualTo(new[] { "kitchenaid 5ksm150" }));
        Assert.That(Fitment.Find("Zahnrad für den Modellbau", 0.75), Is.Empty);
    }

    // S5: money, shipping, pickup and payment phrases are no machines; only title and first sentence are read
    [Test]
    public void SellerTerms_AreNoMachines()
    {
        Assert.That(Fitment.Find("Zahnrad für 5 Euro abzugeben", 0.75), Is.Empty);
        Assert.That(Fitment.Find("Nur für Selbstabholer", 0.75), Is.Empty);
        Assert.That(Fitment.Find("Zahlung per PayPal oder per Überweisung", 0.75), Is.Empty);
        Assert.That(Fitment.Find("Versand für 4,99 EUR", 0.75), Is.Empty);
        Assert.That(Fitment.Find("Zahnrad für bastler", 0.75), Is.Empty, "a lower case generic word is no brand");
        Assert.That(Fitment.Find("zahnrad für bosch gsr 12v", 0.75).Select(f => f.MachineKey), Is.EqualTo(new[] { "bosch gsr 12v" }), "a known brand in a lower case title");

        var fits = Fitment.FindInListing("Zahnrad 20 Zähne", "Passend für Makita HR2470. Versand für 5 Euro, Zahlung per PayPal, nur für Selbstabholer aus Köln.");
        Assert.That(fits.Select(f => f.MachineKey), Is.EqualTo(new[] { "makita hr2470" }));
    }

    [Test]
    public void FromProductModel_ReadsTheDevice()
    {
        Assert.That(Fitment.FromProductModel("Spare part for iPhone 11")!.MachineKey, Is.EqualTo("iphone 11"));
        Assert.That(Fitment.FromProductModel("Galaxy S21"), Is.Null);
    }

    [Test]
    public void MachineKey_FoldsCaseAndPunctuation()
    {
        Assert.That(Fitment.MachineKey("BOSCH GSR 12V-15"), Is.EqualTo(Fitment.MachineKey("bosch gsr 12v-15")));
        Assert.That(Fitment.MachineKey("Bosch  GSR 12V-15"), Is.EqualTo("bosch gsr 12v 15"));
    }

    [Test]
    public void FirstSentence_CutsAtTheSentenceEnd()
    {
        Assert.That(PartText.FirstSentence("Passend für Makita HR2470. Versand 5 Euro."), Is.EqualTo("Passend für Makita HR2470."));
        Assert.That(PartText.FirstSentence("Zeile eins\nZeile zwei"), Is.EqualTo("Zeile eins"));
        Assert.That(PartText.FirstSentence(null), Is.EqualTo(""));
    }
}

[TestFixture]
public class GearToothVerdictTests
{
    private static TextValue<int> Text(int n, double conf = 0.9) => new(n, conf, $"teeth: \"{n} zahne\"");

    [Test]
    public void TextAndPhotoAgree_IsTextAndImage()
    {
        var decision = GearToothVerdict.Decide(Text(42), new GearPhotoMeasurement(42, true, 0.9, null, 0.2));
        Assert.That(decision.Teeth, Is.EqualTo(42));
        Assert.That(decision.Source, Is.EqualTo(PartValueSources.TextAndImage));
        Assert.That(decision.Confidence, Is.GreaterThan(0.9));
    }

    // S10: an unverified or unsure photo never vetoes a clear text
    [Test]
    public void Disagreement_TextWins_UnlessPhotoIsVerifiedAndSure()
    {
        var unverified = GearToothVerdict.Decide(Text(42), new GearPhotoMeasurement(41, false, 0.95, null, null));
        Assert.That(unverified.Teeth, Is.EqualTo(42));
        Assert.That(unverified.Source, Is.EqualTo(PartValueSources.Text));
        Assert.That(unverified.Evidence, Does.Contain("not trusted"));

        var unsure = GearToothVerdict.Decide(Text(42), new GearPhotoMeasurement(41, true, 0.85, null, null));
        Assert.That(unsure.Teeth, Is.EqualTo(42));

        var veto = GearToothVerdict.Decide(Text(42), new GearPhotoMeasurement(41, true, 0.95, null, null));
        Assert.That(veto.IsKnown, Is.False);
        Assert.That(veto.Evidence, Does.Contain("42").And.Contain("41").And.Contain("conflict"));
    }

    [Test]
    public void TextOnly_NeedsClearText()
    {
        Assert.That(GearToothVerdict.Decide(Text(42, 0.9), null).Teeth, Is.EqualTo(42));
        Assert.That(GearToothVerdict.Decide(Text(42, 0.5), null).IsKnown, Is.False);
    }

    [Test]
    public void PhotoOnly_NeedsVerifiedAndConfident()
    {
        Assert.That(GearToothVerdict.Decide(null, new GearPhotoMeasurement(42, true, 0.9, null, null)).Source, Is.EqualTo(PartValueSources.Image));
        Assert.That(GearToothVerdict.Decide(null, new GearPhotoMeasurement(42, false, 0.9, "methods disagree", null)).IsKnown, Is.False);
        Assert.That(GearToothVerdict.Decide(null, new GearPhotoMeasurement(42, true, 0.6, null, null)).IsKnown, Is.False);
        Assert.That(GearToothVerdict.Decide(null, new GearPhotoMeasurement(null, false, 0, "gear touches the image border", null)).Evidence, Does.Contain("border"));
    }

    [Test]
    public void Nothing_IsUnknownWithReason()
    {
        var decision = GearToothVerdict.Decide(null, null, "several tooth counts stated: 42, 12");
        Assert.That(decision.IsKnown, Is.False);
        Assert.That(decision.Evidence, Does.Contain("several"));
    }
}

[TestFixture]
public class PartVoteTests
{
    private static PartMeasurement Reading(string listing, string value, double conf = 0.9) => new()
    {
        SeoId = "p", AttributeKey = PartAttributeKeys.GearTeeth, Source = PartValueSources.Text, ListingKey = listing, Value = value, Numeric = double.Parse(value), Confidence = conf,
    };

    // S6: the value with the most listing support wins; a tie is no fact
    [Test]
    public void Majority_Wins_TieIsUnknown()
    {
        var two = PartVote.Decide([Reading("a", "42"), Reading("b", "40")]);
        Assert.That(two.IsKnown, Is.False);
        Assert.That(two.Evidence, Does.Contain("disagree"));

        var three = PartVote.Decide([Reading("a", "42"), Reading("b", "40"), Reading("c", "42")]);
        Assert.That(three.Value, Is.EqualTo("42"));
        Assert.That(three.Support, Is.EqualTo(2));
        Assert.That(three.Against, Is.EqualTo(1));
        Assert.That(three.Confidence, Is.LessThan(0.9).And.GreaterThan(0.5));

        var lastWins = PartVote.Decide([Reading("a", "42"), Reading("b", "42"), Reading("c", "40")]);
        Assert.That(lastWins.Value, Is.EqualTo("42"), "the last listing does not win, the majority does");
    }

    [Test]
    public void ReviewRows_DoNotVote_OneListingIsFullConfidence()
    {
        var one = PartVote.Decide([Reading("a", "42", 0.85), new PartMeasurement { AttributeKey = PartAttributeKeys.GearTeeth, Source = PartValueSources.Review, Value = "unknown" }]);
        Assert.That(one.Value, Is.EqualTo("42"));
        Assert.That(one.Confidence, Is.EqualTo(0.85));
        Assert.That(PartVote.Decide([]).IsKnown, Is.False);
    }
}

[TestFixture]
public class PartMatchingTests
{
    private static PartSignatureRow Row(string id, int? teeth, double? od = null, double? bore = null, double? module = null, string? pn = null, string? material = null, bool labelled = true) => new()
    {
        PartClass = PartClasses.Gear, PartKey = teeth?.ToString() ?? PartSignatureRow.UnknownKey, Teeth = teeth, SeoId = id, Name = id, OuterDiameterMm = od, BoreMm = bore, Module = module,
        PartNumberKey = pn == null ? null : PartNumbers.Key(pn), PartNumber = pn, PartNumberLabelled = pn != null && labelled, Material = material,
    };

    [Test]
    public void Score_TeethExact_SizeWithinTolerance()
    {
        var query = new PartQuery(PartClasses.Gear, 42, 35, null, null, null, null, null, PartTolerance.Default);
        Assert.That(PartSignatureMatch.Score(Row("a", 42, 35), query), Is.EqualTo(1));
        Assert.That(PartSignatureMatch.Score(Row("b", 42, 37), query), Is.GreaterThan(0).And.LessThan(1));
        Assert.That(PartSignatureMatch.Score(Row("c", 42, 45), query), Is.EqualTo(0), "outside 8 %");
        Assert.That(PartSignatureMatch.Score(Row("d", 41, 35), query), Is.EqualTo(0), "teeth are exact");
        Assert.That(PartSignatureMatch.Score(Row("e", 42), query), Is.GreaterThan(0), "an unknown size does not exclude");
    }

    [Test]
    public void Score_TeethRange_RanksByCloseness()
    {
        var query = new PartQuery(PartClasses.Gear, 24, null, null, null, null, null, null, PartTolerance.Default, TeethMin: 22, TeethMax: 26);
        Assert.That(query.Keys(), Is.EqualTo(new[] { "22", "23", "24", "25", "26" }));
        Assert.That(PartSignatureMatch.Score(Row("exact", 24), query), Is.EqualTo(1));
        Assert.That(PartSignatureMatch.Score(Row("near", 25), query), Is.LessThan(1).And.GreaterThan(PartSignatureMatch.Score(Row("far", 26), query)));
        Assert.That(PartSignatureMatch.Score(Row("out", 27), query), Is.EqualTo(0));
    }

    [Test]
    public void Score_BearingByDesignation()
    {
        var row = new PartSignatureRow { PartClass = PartClasses.Bearing, PartKey = "6203-2RS", SeoId = "b", InnerDiameterMm = 17, OuterDiameterMm = 40 };
        var query = new PartQuery(PartClasses.Bearing, null, null, null, null, null, null, null, PartTolerance.Default, PartKey: "6203-2RS", InnerDiameterMm: 17);
        Assert.That(PartSignatureMatch.Score(row, query), Is.EqualTo(1));
        Assert.That(PartSignatureMatch.Score(row, query with { PartKey = "6204" }), Is.EqualTo(0));
    }

    [Test]
    public void Tolerance_HasAnAbsoluteFloor()
    {
        Assert.That(PartTolerance.Default.Within(10, 11.2), Is.True);
        Assert.That(PartTolerance.Default.Within(10, 12), Is.False);
        Assert.That(PartTolerance.Default.Within(100, 107), Is.True);
    }

    // S9: the same-part tolerance is relative (2 %) with a 0.2 mm floor
    [Test]
    public void SamePartTolerance_IsRelative()
    {
        Assert.That(PartTolerance.SamePart.Within(8, 9), Is.False, "an 8 and a 9 mm bore are different shafts");
        Assert.That(PartTolerance.SamePart.Within(8, 8.15), Is.True);
        Assert.That(PartTolerance.SamePart.Within(100, 101.5), Is.True);
        Assert.That(PartTolerance.SamePart.Within(100, 103), Is.False);
    }

    // S8: confirmed only on a labelled part number or an identical triple
    [Test]
    public void SamePartNumber_IsConfirmedOnlyWhenLabelled()
    {
        var edge = SamePartRules.Evaluate(Row("a", 42, pn: "1 619 P08 924"), Row("b", null, pn: "1619p08924"));
        Assert.That(edge!.Kind, Is.EqualTo(PartEdgeKinds.PartNumber));
        Assert.That(edge.Status, Is.EqualTo(PartEdgeKinds.Confirmed));
        Assert.That(edge.Confidence, Is.EqualTo(0.95));

        var bare = SamePartRules.Evaluate(Row("a", 42, pn: "1600A00F7V", labelled: false), Row("b", null, pn: "1600A00F7V", labelled: false));
        Assert.That(bare!.Status, Is.EqualTo(PartEdgeKinds.Candidate), "a bare 0.6 code never confirms");
        Assert.That(bare.Confidence, Is.LessThan(0.95));

        var secondary = SamePartRules.PartNumberEdge("a", "b", new PartNumberRow { PartNumber = "X-12345", Labelled = true, Primary = false }, new PartNumberRow { PartNumber = "X-12345", Labelled = true, Primary = true });
        Assert.That(secondary.Status, Is.EqualTo(PartEdgeKinds.Candidate), "a secondary part number makes a candidate");
    }

    [Test]
    public void IdenticalTriple_IsConfirmed_SignatureAloneIsACandidate()
    {
        var triple = SamePartRules.Evaluate(Row("a", 42, 44, 8, 1), Row("b", 42, 44.5, 8, 1));
        Assert.That(triple!.Status, Is.EqualTo(PartEdgeKinds.Confirmed));
        Assert.That(triple.Confidence, Is.EqualTo(0.9));
        Assert.That(triple.Evidence, Does.Contain("identical triple"));

        Assert.That(SamePartRules.Evaluate(Row("a", 42, 35), Row("b", 42, 35)), Is.Null, "teeth and one measure are not enough");
        var edge = SamePartRules.Evaluate(Row("a", 42, 35, 8), Row("b", 42, 35.5, 8));
        Assert.That(edge!.Kind, Is.EqualTo(PartEdgeKinds.Signature));
        Assert.That(edge.Status, Is.EqualTo(PartEdgeKinds.Candidate));
        Assert.That(edge.Confidence, Is.EqualTo(0.5).Within(0.001));
        Assert.That(edge.Evidence, Does.Contain("teeth 42"));

        var stronger = SamePartRules.Evaluate(Row("a", 42, 35, 8, material: "brass"), Row("b", 42, 35, 8, material: "brass"));
        Assert.That(stronger!.Confidence, Is.GreaterThan(edge.Confidence));
        Assert.That(stronger.Status, Is.EqualTo(PartEdgeKinds.Candidate), "without the module the triple is not complete");
    }

    [Test]
    public void DifferentModuleOrTeeth_NoEdge()
    {
        Assert.That(SamePartRules.Evaluate(Row("a", 42, 35, 8, 1), Row("b", 42, 35, 8, 1.5)), Is.Null);
        Assert.That(SamePartRules.Evaluate(Row("a", 42, 35, 8), Row("b", 43, 35, 8)), Is.Null);
        Assert.That(SamePartRules.Evaluate(Row("a", null, 35, 8), Row("b", null, 35, 8)), Is.Null, "unknown teeth give no signature edge");
        Assert.That(SamePartRules.Evaluate(Row("a", 42, 35, 8), Row("b", 42, 35, 9)), Is.Null, "a bore that disagrees by 1 mm blocks");
    }
}

[TestFixture]
public class GearEstimateTests
{
    // reverse search by a broken gear
    [Test]
    public void Fragment_EightTeethOver120Degrees_Is24()
    {
        var e = GearEstimate.FromFragment(8, 120)!;
        Assert.That(e.Teeth, Is.EqualTo(24));
        Assert.That(e.Min, Is.EqualTo(22));
        Assert.That(e.Max, Is.EqualTo(26));
        Assert.That(GearEstimate.FromFragment(5, 90)!.Teeth, Is.EqualTo(20));
        Assert.That(GearEstimate.FromFragment(1, 120), Is.Null);
        Assert.That(GearEstimate.FromFragment(8, 5), Is.Null);
    }

    [Test]
    public void Pitch_AndModule()
    {
        var p = GearEstimate.FromPitch(3.14, 60)!;
        Assert.That(p.Teeth, Is.EqualTo(60));
        Assert.That(p.Min, Is.EqualTo(58));
        var m = GearEstimate.FromModule(1, 44)!;
        Assert.That(m.Teeth, Is.EqualTo(42));
        Assert.That(m.Min, Is.EqualTo(41));
        Assert.That(m.Max, Is.EqualTo(43));
        Assert.That(GearEstimate.FromModule(0, 44), Is.Null);
    }
}

[TestFixture]
public class PartStoreTests
{
    [Test]
    public async Task InMemory_SignatureFitsEdges_RoundTrip()
    {
        var store = new InMemoryPartStore();
        await store.UpsertSignatureAsync(new PartSignatureRow { PartClass = "gear", PartKey = "42", Teeth = 42, SeoId = "a" });
        await store.UpsertSignatureAsync(new PartSignatureRow { PartClass = "gear", PartKey = "42", Teeth = 42, SeoId = "b" });
        await store.UpsertFitAsync(new PartFitRow { MachineKey = "bosch gsr 12v", SeoId = "a", MachineName = "Bosch GSR 12V" });
        await store.UpsertSameEdgeAsync(new PartSameEdge { SeoId = "a", OtherSeoId = "b", Kind = "signature", Status = "candidate", Confidence = 0.5 });

        Assert.That((await store.GetBySignatureAsync("gear", "42", 10)).Select(r => r.SeoId), Is.EqualTo(new[] { "a", "b" }));
        Assert.That((await store.GetBySignatureAsync("gear", "41", 10)), Is.Empty);
        Assert.That((await store.GetPartsForMachineAsync("bosch gsr 12v", 10)).Single().SeoId, Is.EqualTo("a"));
        Assert.That((await store.GetMachinesForPartAsync("a")).Single().MachineName, Is.EqualTo("Bosch GSR 12V"));
        Assert.That((await store.GetSameEdgesAsync("b")).Single().OtherSeoId, Is.EqualTo("a"), "edges are written in both directions");

        await store.DeleteSignatureAsync("gear", "42", "a");
        Assert.That((await store.GetBySignatureAsync("gear", "42", 10)).Select(r => r.SeoId), Is.EqualTo(new[] { "b" }));
        await store.DeleteSameEdgeAsync("a", "b");
        Assert.That(await store.GetSameEdgesAsync("b"), Is.Empty, "deleted in both directions");
        await store.DeleteFitAsync("bosch gsr 12v", "a");
        Assert.That(await store.GetMachinesForPartAsync("a"), Is.Empty);
    }

    [Test]
    public async Task InMemory_Measurements_KeepListingsAndSourcesSideBySide()
    {
        var store = new InMemoryPartStore();
        await store.UpsertMeasurementsAsync([
            new PartMeasurement { SeoId = "a", AttributeKey = PartAttributeKeys.GearTeeth, Source = PartValueSources.Text, ListingKey = "1:l1", Value = "42", Confidence = 0.9 },
            new PartMeasurement { SeoId = "a", AttributeKey = PartAttributeKeys.GearTeeth, Source = PartValueSources.Text, ListingKey = "1:l2", Value = "40", Confidence = 0.9 },
            new PartMeasurement { SeoId = "a", AttributeKey = PartAttributeKeys.GearTeeth, Source = PartValueSources.Image, ListingKey = "1:l1", Value = "41", Confidence = 0.8 },
        ]);
        Assert.That((await store.GetMeasurementsAsync("a")), Has.Count.EqualTo(3), "one row per listing and source");
        await store.DeleteMeasurementsAsync("a", [PartAttributeKeys.GearTeeth]);
        Assert.That(await store.GetMeasurementsAsync("a"), Is.Empty);
    }

    [Test]
    public void CassandraMapping_NamesEveryTable()
    {
        var mapping = CassandraPartStore.BuildMapping();
        Assert.That(mapping.Get<PartPageState>().TableName, Is.EqualTo("part_pages"));
        Assert.That(mapping.Get<PartMeasurement>().TableName, Is.EqualTo("part_measurements"));
        Assert.That(mapping.Get<PartSignatureRow>().TableName, Is.EqualTo("parts_by_signature"));
        Assert.That(mapping.Get<PartNumberRow>().TableName, Is.EqualTo("part_numbers"));
        Assert.That(mapping.Get<PartFitRow>().TableName, Is.EqualTo("part_fits"));
        Assert.That(mapping.Get<CassandraPartStore.PartFitByPartRow>().TableName, Is.EqualTo("part_fits_by_part"));
        Assert.That(mapping.Get<PartSameEdge>().TableName, Is.EqualTo("part_same_edges"));
        Assert.That(mapping.Get<PartSignatureRow>().PartitionKeys, Is.EqualTo(new[] { "part_class", "part_key" }));
        Assert.That(mapping.Get<PartMeasurement>().ClusteringKeys.Select(c => c.Item1), Is.EqualTo(new[] { "attribute_key", "source", "listing_key" }));
        Assert.That(CassandraPartStore.DefaultTtlSeconds, Is.EqualTo(120 * 24 * 3600));
    }
}
