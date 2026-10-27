using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductAliasIndexTests
{
    private static KnownProduct Product(string id, string alias) => new()
    {
        Id = id, Brand = "Nintendo", Name = id, Model = id,
        Aliases = new(StringComparer.OrdinalIgnoreCase) { alias }
    };

    [TestCase("Switch")]
    [TestCase("Used Switch")]
    [TestCase("Switch used")]
    public void SingleTokenAlias_MatchesAtEndOrBeforeUnrelatedToken(string title)
    {
        var product = Product("base", "Switch");
        var matcher = new KnownProductMatcher([product]);
        Assert.That(matcher.Match(title), Is.SameAs(product));
    }

    [Test]
    public void LongerAlias_WinsOverSingleTokenAndSharedPairPrefixes()
    {
        var specific = Product("oled", "Switch OLED White");
        var matcher = new KnownProductMatcher([Product("base", "Switch"), Product("oled-family", "Switch OLED"), specific]);
        Assert.That(matcher.Match("Used Switch OLED White"), Is.SameAs(specific));
    }

    [TestCase("Used Switch OLED White Limited", true)]
    [TestCase("Used Switch OLED White Other", false)]
    public void FourthAliasToken_StillRequiresFullMatch(string title, bool matches)
    {
        var product = Product("limited", "Switch OLED White Limited");
        var matcher = new KnownProductMatcher([product]);
        Assert.That(matcher.Match(title), matches ? Is.SameAs(product) : Is.Null);
    }

    [TestCase("Switch")]
    [TestCase("Switch OLED")]
    public void EquallySpecificAliasesForDifferentProducts_RemainAmbiguous(string alias)
    {
        var matcher = new KnownProductMatcher([Product("first", alias), Product("second", alias)]);
        Assert.That(matcher.Match("Used " + alias), Is.Null);
    }
}
