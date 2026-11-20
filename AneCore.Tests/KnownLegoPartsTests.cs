using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownLegoPartsTests
{
    [Test]
    public void PlainPartHasNameCategoryAndSetCollisionFlag()
    {
        var brick = KnownLegoParts.ForPartNumber("3001")!;
        Assert.That(brick.Name, Is.EqualTo("Brick 2 x 4"));
        Assert.That(brick.Category, Is.EqualTo("Bricks"));
        // 3001 is also the Rebrickable set "Kabaya Promotional Set: Propeller Buggy"
        Assert.That(brick.SharesSetNumber, Is.True);
        Assert.That(KnownLegoParts.ForPartNumber("3069B")?.Name, Is.EqualTo("Tile 1 x 2 with Groove"));
        Assert.That(KnownLegoParts.ForPartNumber("32523")?.SharesSetNumber, Is.False);
    }

    [TestCase("3626cpr0001")]
    [TestCase("3068bpat0001")]
    [TestCase("973")]
    [TestCase("99999999")]
    public void PrintsPatternsShortAndUnknownNumbersAreNoParts(string number) =>
        Assert.That(KnownLegoParts.ForPartNumber(number), Is.Null);

    [Test]
    public void ElementIdGivesPartAndColour()
    {
        Assert.That(KnownLegoParts.ForElementId("300121"), Is.EqualTo(new LegoElement("300121", "3001", 4)));
        Assert.That(KnownLegoParts.ForElementId("1234"), Is.Null);
    }

    [Test]
    public void OldAndNewGreysAreDifferentColoursWithoutSharedPhrases()
    {
        var oldGrey = KnownLegoParts.Color(7)!;
        var newGrey = KnownLegoParts.Color(71)!;
        Assert.That((oldGrey.Name, newGrey.Name), Is.EqualTo(("Light Gray", "Light Bluish Gray")));
        Assert.That(newGrey.Slug, Is.EqualTo("light-bluish-gray"));
        Assert.That(oldGrey.AllTerms.Intersect(newGrey.AllTerms), Is.Empty);
        Assert.That(newGrey.TermsFor("de"), Does.Contain("neuhellgrau"));
        Assert.That(KnownLegoParts.AmbiguousColorWords, Does.Contain("hellgrau").And.Contain("light gray"));
    }

    [Test]
    public void RedHasAReviewedWordInEveryLanguage()
    {
        var red = KnownLegoParts.Color(4)!;
        Assert.That(red.TermsFor("de"), Does.Contain("rot"));
        Assert.That(red.TermsFor("fr"), Does.Contain("rouge"));
        Assert.That(red.TermsFor("nl"), Does.Contain("rood"));
        Assert.That(red.TermsFor("it"), Does.Contain("rosso"));
        Assert.That(red.TermsFor("es"), Does.Contain("rojo"));
        Assert.That(red.TermsFor("pl"), Does.Contain("czerwony"));
        Assert.That(KnownLegoParts.Colors.Values.Select(c => c.Slug).Distinct().Count(), Is.EqualTo(KnownLegoParts.Colors.Count));
    }

    [Test]
    public void SearchTextCarriesNameCategoryTranslatedPartWordsAndColour()
    {
        var text = KnownLegoParts.SearchText(KnownLegoParts.ForPartNumber("3001")!, KnownLegoParts.Color(4));
        Assert.That(text, Does.Contain("Brick 2 x 4").And.Contain("Bricks").And.Contain("Stein").And.Contain("Red").And.Contain("rot"));
        Assert.That(KnownLegoParts.SearchText(KnownLegoParts.ForPartNumber("3023")!, null), Does.Contain("Platte").And.Not.Contain("rot"));
    }

    [TestCase("navy")]
    [TestCase("navy blue")]
    [TestCase("navyblau")]
    [TestCase("navy blau")]
    public void NavyIsDarkBlue(string phrase) =>
        Assert.That(KnownLegoParts.Color(272)!.AllTerms, Does.Contain(phrase));

    [TestCase("old gray")]
    [TestCase("old grey")]
    [TestCase("altgrau")]
    public void UndecidedOldGreyNamesNoColourButIsAmbiguous(string phrase)
    {
        // "old gray" is old Light Gray (7) or old Dark Gray (8): a seller's plain word does not say which
        Assert.That(KnownLegoParts.Colors.Values.SelectMany(c => c.AllTerms), Does.Not.Contain(phrase));
        Assert.That(KnownLegoParts.AmbiguousColorWords, Does.Contain(phrase));
    }
}
