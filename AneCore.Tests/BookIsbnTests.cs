using NUnit.Framework;

namespace Coflnet.Ane.Tests;

public class BookIsbnTests
{
    [TestCase("9780306406157", "9780306406157")]
    [TestCase("978-0-306-40615-7", "9780306406157")]
    [TestCase("0-306-40615-2", "9780306406157")]
    [TestCase("0 306 40615 2", "9780306406157")]
    [TestCase("0-8044-2957-X", "9780804429573")]
    [TestCase("0-8044-2957-x", "9780804429573")]
    public void EquivalentIsbnsHaveOneCanonicalIdentity(string input, string expected)
        => Assert.That(BookIsbn.Normalize(input), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("9780306406150")]
    [TestCase("0306406150")]
    [TestCase("97803064061578")]
    [TestCase("9780306406157 8")]
    [TestCase("4006381333931")]
    [TestCase("ISBN 9780306406157")]
    public void NonIsbnsAndInvalidClaimsNeverBecomeEditionIdentities(string? input)
        => Assert.That(BookIsbn.Normalize(input), Is.Null);
}
