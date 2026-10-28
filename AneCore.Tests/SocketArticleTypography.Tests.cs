using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class SocketArticleTypographyTests
{
    private static readonly KnownProductMatcher Matcher = new(KnownProductSeed.LoadAll(true));

    // These seller typography forms are already accepted by Normalize; no new aliases are authored.
    [TestCase("Bahco 6700 SM-4.8", "bahco 6700 sm 4 8")]
    [TestCase("Bahco 6700SM-4,5foo", "bahco 6700 sm 4 5 foo")]
    [TestCase("Bahco 6700/SM-4.8", "bahco 6700 sm 4 8")]
    [TestCase("Bahco 6700:SM-4.8", "bahco 6700 sm 4 8")]
    [TestCase("Bahco 6700 SM-4,5", "bahco 6700 sm 4 5")]
    public void ActualNormalizerExplainsTheCandidateTokens(string title, string expected) =>
        Assert.That(KnownProductMatcher.Normalize(title), Is.EqualTo(expected));

    [TestCase("Bahco 6700 SM-4.8")]
    [TestCase("Bahco6700 SM-4,8")]
    [TestCase("Bahco 6700SM-4,5foo")]
    [TestCase("Bahco 6700 SM 4,5foo")]
    [TestCase("Bahco 6700/SM-4.8")]
    [TestCase("Bahco 6700:SM-4.8")]
    [TestCase("Bahco 7800 SM 13FOO")]
    [TestCase("Bahco 7800/SM/13/10")]
    [TestCase("Bahco 7800.SM.13FOO")]
    [TestCase("Bahco6700SM_4,5foo")]
    public void UnknownDecimalOrAttachedVariantCannotBorrowTheShorterSocket(string title)
    {
        Assert.That(Matcher.Match(title), Is.Null, title);
        Assert.That(ResaleCatalog.Match(title), Is.Null, title);
    }

    [TestCase("Bahco 6700 SM-4.5", "bahco-6700sm-4-5")]
    [TestCase("Bahco 6700SM-4,5", "bahco-6700sm-4-5")]
    [TestCase("Bahco 6700 SM-4,5", "bahco-6700sm-4-5")]
    [TestCase("Bahco 6700/SM-4.5", "bahco-6700sm-4-5")]
    [TestCase("Bahco6700:SM-4,5mm", "bahco-6700sm-4-5")]
    [TestCase("Bahco 7800 SM 13mm", "bahco-7800sm-13")]
    [TestCase("Bahco 7800/SM/13", "bahco-7800sm-13")]
    [TestCase("Bahco 6700_SM_4.5", "bahco-6700sm-4-5")]
    [TestCase("Bahco 7800 SM-13 mit Koffer", "bahco-7800sm-13")]
    public void KnownArticleUnitsAndIncludedCaseKeepExistingNormalizedMatches(string title, string id)
    {
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id), title);
        Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id), title);
    }
}
