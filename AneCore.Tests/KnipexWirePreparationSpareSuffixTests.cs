using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnipexWirePreparationSpareSuffixTests
{
    private static readonly KnownProductMatcher Matcher = new(KnownProductSeed.LoadAll(true));

    [TestCase("12 74 180")]
    [TestCase("12 64 180")]
    [TestCase("16 45 220")]
    public void CompleteArticleStillMatches(string article) =>
        Assert.That(Matcher.Match("KNIPEX " + article)?.Id, Is.EqualTo("knipex-" + article.Replace(" ", "")));

    [TestCase("12 74 180", false)]
    [TestCase("12 74 180", true)]
    [TestCase("12 64 180", false)]
    [TestCase("12 64 180", true)]
    [TestCase("16 45 220", false)]
    [TestCase("16 45 220", true)]
    public void ManufacturerSpareBladeCodeCannotFallBackToCompleteBody(string article, bool compact)
    {
        var code = compact ? article.Replace(" ", "") : article;
        Assert.That(Matcher.Match("KNIPEX " + code + " E01"), Is.Null,
            "the manufacturer identifies this exact suffix as spare blades, not the complete tool");
    }
}
