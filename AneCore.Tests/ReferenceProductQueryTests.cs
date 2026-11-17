using Coflnet.Ane.ReferenceListings;

namespace AneCore.Tests;

public sealed class ReferenceProductQueryTests
{
    [TestCase("Xiaomi 14 T pro Smartphone - Neuwertig in OVP", "Xiaomi 14T pro")]
    [TestCase("Canon EOS - 5D Mark II", "Canon EOS 5D Mark II")]
    [TestCase("LEGO Star Wars - 75192 Millennium Falcon", "LEGO Star Wars 75192 Millennium Falcon")]
    [TestCase("Zelda Tears of the Kingdom - Collector's Edition", "Zelda Tears of the Kingdom Collector's Edition")]
    [TestCase("Glurak Base Set - 1st Edition - PSA 10", "Glurak Base Set 1st Edition PSA 10")]
    [TestCase("Top Gun Maverick Blu-ray", "Top Gun Maverick Blu-ray")]
    [TestCase("Made in Abyss Band 1", "Made in Abyss Band 1")]
    [TestCase("New Nintendo 3DS XL", "New Nintendo 3DS XL")]
    [TestCase("New Super Mario Bros", "New Super Mario Bros")]
    [TestCase("Nike Crop Top", "Nike Crop Top")]
    [TestCase("iPhone 12 - defekt", "iPhone 12 defekt")]
    [TestCase("PS5 | Kleinanzeigen", "PS5")]
    [TestCase("Sting - Brand New Day CD", "Sting Brand New Day CD")]
    [TestCase("Weezer - Like New Vinyl", "Weezer Like New Vinyl")]
    [TestCase("Handy Manny DVD", "Handy Manny DVD")]
    [TestCase("IKEA Malm Bett 140 x 200 - weiß", "IKEA Malm Bett 140 x 200 weiß")]
    [TestCase("Samsung S23 like new", "Samsung S23")]
    [TestCase("Samsung S23 brand new - OVP", "Samsung S23")]
    [TestCase("Neu in OVP", "Neu in OVP")]
    [TestCase("iPhone 13 Pro 256GB Graphit - Top Zustand", "iPhone 13 Pro 256GB Graphit")]
    [TestCase("Samsung Galaxy S23 Ultra wie neu mit OVP", "Samsung Galaxy S23 Ultra")]
    [TestCase("iPhone 11 Handy - 128GB, schwarz, gebraucht", "iPhone 11 128GB schwarz")]
    [TestCase("Sony PS5 like new boxed", "Sony PS5")]
    [TestCase("PS5 - Neu", "PS5")]
    [TestCase("Pokemon Karte sealed steelbook - Neu und OVP", "Pokemon Karte sealed steelbook")]
    [TestCase("Nintendo Switch OLED 5 G", "Nintendo Switch OLED 5G")]
    public void BuildsProductQueryFromTitle(string title, string expected)
        => Assert.That(ReferenceProductQuery.FromTitle(title), Is.EqualTo(expected));

    [Test]
    public void EmptyTitleGivesEmptyQuery() => Assert.That(ReferenceProductQuery.FromTitle("  "), Is.EqualTo(""));
}
