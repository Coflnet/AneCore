using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class PokemonNamesTests
{
    [Test]
    public void TryGetEnglishName_KnownGermanName_ReturnsEnglish()
    {
        Assert.That(PokemonNames.TryGetEnglishName("glurak"), Is.EqualTo("Charizard"));
    }

    [Test]
    public void TryGetEnglishName_IsCaseInsensitive()
    {
        Assert.That(PokemonNames.TryGetEnglishName("GLURAK"), Is.EqualTo("Charizard"));
        Assert.That(PokemonNames.TryGetEnglishName("Glurak"), Is.EqualTo("Charizard"));
    }

    [Test]
    public void TryGetEnglishName_IgnoresDiacritics()
    {
        // "glurak" itself carries no accent, but a caller could still pass an accented variant
        // (autocorrect, a differently-encoded scrape); the lookup must fold it before matching.
        Assert.That(PokemonNames.TryGetEnglishName("glüräk"), Is.EqualTo("Charizard"));
    }

    [Test]
    public void TryGetEnglishName_EnglishName_ReturnsItself()
    {
        Assert.That(PokemonNames.TryGetEnglishName("charizard"), Is.EqualTo("Charizard"));
    }

    [Test]
    public void TryGetEnglishName_SharedGermanEnglishName_ReturnsItself()
    {
        Assert.That(PokemonNames.TryGetEnglishName("pikachu"), Is.EqualTo("Pikachu"));
    }

    [Test]
    public void TryGetEnglishName_UnknownName_ReturnsNull()
    {
        Assert.That(PokemonNames.TryGetEnglishName("not-a-pokemon"), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void TryGetEnglishName_BlankInput_ReturnsNull(string? input)
    {
        Assert.That(PokemonNames.TryGetEnglishName(input), Is.Null);
    }

    [Test]
    public void TryGetEnglishName_LeadingTrailingWhitespace_IsTrimmed()
    {
        Assert.That(PokemonNames.TryGetEnglishName("  glurak  "), Is.EqualTo("Charizard"));
    }

    [Test]
    public void TryGetEnglishName_HyphenatedName_MatchesExactly()
    {
        Assert.That(PokemonNames.TryGetEnglishName("ho-oh"), Is.EqualTo("Ho-Oh"));
    }

    [Test]
    public void GermanToEnglish_HasNoNullOrEmptyValues()
    {
        Assert.That(PokemonNames.GermanToEnglish.Values, Has.None.Null.Or.Empty);
    }

    [Test]
    public void GermanToEnglish_LookupIsCaseInsensitiveDictionaryWide()
    {
        // Regression: a dictionary built with the default comparer would silently return the wrong
        // (or no) entry for anything but the exact stored casing.
        Assert.That(PokemonNames.GermanToEnglish.TryGetValue("GlUrAk", out var english), Is.True);
        Assert.That(english, Is.EqualTo("Charizard"));
    }
}
