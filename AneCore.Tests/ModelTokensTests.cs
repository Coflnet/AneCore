using Coflnet.Ane.Text;

namespace AneCore.Tests;

public sealed class ModelTokensTests
{
    [TestCase("Xiaomi 14 T pro", "Xiaomi 14T pro")]
    [TestCase("xiaomi 14 t pro", "xiaomi 14t pro")]
    [TestCase("5 g", "5g")]
    [TestCase("Samsung 5 G Handy", "Samsung 5G Handy")]
    [TestCase("14 t", "14t")]
    [TestCase("Xiaomi 14 T", "Xiaomi 14T")]
    [TestCase("14 T pro und 13 T", "14T pro und 13T")]
    [TestCase("14 t, pro", "14t, pro")]
    [TestCase("Galaxy S23 ultra", "Galaxy S23 ultra")]
    [TestCase("a 14", "a 14")]
    [TestCase("iPhone 14 pro", "iPhone 14 pro")]
    [TestCase("Ärmel 14 ö", "Ärmel 14ö")]
    [TestCase("14 t-shirt", "14 t-shirt")]
    [TestCase("14 ti", "14 ti")]
    [TestCase("1.5 m", "1.5 m")]
    [TestCase("Nr, 5 b", "Nr, 5b")]
    [TestCase("Xiaomi 14\u00A0T pro", "Xiaomi 14T pro")]
    [TestCase("Bett 140 x 200", "Bett 140 x 200")]
    [TestCase("2 X Controller", "2 X Controller")]
    [TestCase("", "")]
    public void JoinsNumberAndStandaloneLetter(string input, string expected)
        => Assert.That(ModelTokens.JoinModelSuffix(input), Is.EqualTo(expected));

    [Test]
    public void NullIsEmpty() => Assert.That(ModelTokens.JoinModelSuffix(null), Is.EqualTo(string.Empty));

    [TestCase("14t", true)]
    [TestCase("s23u", true)]
    [TestCase("14", false)]
    [TestCase("pro", false)]
    [TestCase("a14", false)]
    [TestCase("14tb", false)]
    public void RecognisesModelSuffixTokens(string token, bool expected)
        => Assert.That(ModelTokens.IsModelSuffixToken(token), Is.EqualTo(expected));
}
