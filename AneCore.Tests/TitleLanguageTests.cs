using Coflnet.Ane;

namespace AneCore.Tests;

public class TitleLanguageTests
{
    [TestCase("AirPods Pro con custodia di ricarica e scatola", "it")]
    [TestCase("Auricolari Apple AirPods Pro con custodia", "it")]
    [TestCase("air pods pro originali made in california", "it")]
    [TestCase("Airpods PRO per IPhone e Ipad", "it")]
    [TestCase("Air pods pro d’origine neuf.", "fr")]
    [TestCase("AirPod Pro première génération", "fr")]
    [TestCase("Nike Air Max talla 42 nuevo", "es")]
    [TestCase("Sneakers maat 40 zgan", "nl")]
    [TestCase("Buty Nike rozmiar 42", "pl")]
    public void GuessesClearlyForeignTitles(string title, string language)
        => Assert.That(TitleLanguage.Guess(title), Is.EqualTo(language));

    [TestCase("Apple AirPods Pro mit Ladecase und OVP Neu")]
    [TestCase("AirPods Pro zum Verkauf🎧")]
    [TestCase("Airpods pro")]
    [TestCase("AirPods Pro Max")]
    [TestCase("Apple AirPods Pro In-Ear Kopfhörer")]
    [TestCase("Camicia donna")]
    [TestCase("Apple AirPods Pro con case")]
    [TestCase("AirPods Pro per")]
    [TestCase("AirPods Pro neuf mit Ladecase")]
    [TestCase("")]
    [TestCase(null)]
    public void StaysNullForGermanBareOrAmbiguousTitles(string? title)
        => Assert.That(TitleLanguage.Guess(title), Is.Null);

    [Test]
    public void MapsLanguagesToCountries()
    {
        Assert.That(TitleLanguage.CountryFor("it"), Is.EqualTo("IT"));
        Assert.That(TitleLanguage.CountryFor("fr"), Is.EqualTo("FR"));
        Assert.That(TitleLanguage.CountryFor(null), Is.Null);
    }
}
