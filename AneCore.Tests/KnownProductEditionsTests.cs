using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// Collector editions are refined from the base device match (see <see cref="KnownProductEditions"/>): edition words anywhere in the title select the edition,
/// a game named behind an inclusion word or a negated edition keeps the base device, and two editions named equally well keep it too.
/// </summary>
[TestFixture]
public class KnownProductEditionsTests
{
    private static KnownProduct Product(string id, params string[] aliases) => new()
    {
        Id = id, Brand = "Nintendo", Model = id, Name = id, Categories = new() { "Elektronik", "Videospielkonsolen" }, Vertical = "electronics",
        Aliases = new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase),
    };

    private static KnownProduct Edition(string id, string baseId, params string[] terms)
    {
        var product = Product(id, "catalogue " + id);
        product.PossibleAttributes[KnownProductEditions.EditionOfKey] = new HashSet<string> { baseId };
        product.PossibleAttributes[KnownProductEditions.EditionTermsKey] = new HashSet<string>(terms);
        return product;
    }

    private static readonly KnownProductMatcher Synthetic = new(new[]
    {
        Product("switch-oled", "Nintendo Switch OLED", "Switch OLED"),
        Edition("switch-oled-zelda", "switch-oled", "zelda|tears of the kingdom", KnownProductEditions.MarkerRequirement),
        Edition("switch-oled-splatoon", "switch-oled", "splatoon", KnownProductEditions.MarkerRequirement),
        Product("deck-oled", "Steam Deck OLED"),
        Edition("deck-oled-limited", "deck-oled", "limited|smoke"),
        Edition("deck-oled-limited-white", "deck-oled", "white|weiss", "limited|1 tb"),
    });

    private static readonly KnownProductMatcher Real = new(KnownProductSeed.LoadAll(includeExpandedCatalog: true));

    [TestCase("Nintendo Switch OLED Zelda Edition", "switch-oled-zelda")]
    [TestCase("Nintendo Switch OLED Modell 64GB Tears of the Kingdom Edition OVP", "switch-oled-zelda")]
    [TestCase("Zelda Edition Nintendo Switch OLED", "switch-oled-zelda")]
    [TestCase("Nintendo Switch OLED Zelda Edition + Splatoon 3", "switch-oled-zelda")]
    [TestCase("Nintendo Switch OLED Splatoon Sonderedition", "switch-oled-splatoon")]
    [TestCase("Steam Deck OLED 1TB Limited Edition", "deck-oled-limited")]
    [TestCase("Steam Deck OLED 1TB Limited Edition White", "deck-oled-limited-white")]
    [TestCase("Steam Deck OLED 1TB weiß", "deck-oled-limited-white")]
    public void EditionWordsSelectTheEdition(string title, string expected)
        => Assert.That(Synthetic.Match(title)?.Id, Is.EqualTo(expected));

    [TestCase("Nintendo Switch OLED weiß")]
    [TestCase("Nintendo Switch OLED Zelda")]
    [TestCase("Nintendo Switch OLED + Zelda Tears of the Kingdom Edition")]
    [TestCase("Nintendo Switch OLED mit Zelda Collector's Edition")]
    [TestCase("Nintendo Switch OLED weiß Zelda Collector's Edition")]
    [TestCase("Nintendo Switch OLED nicht die Zelda Edition")]
    [TestCase("Nintendo Switch OLED keine Zelda Limited Edition")]
    [TestCase("Nintendo Switch OLED Zelda Splatoon Edition")]
    public void WithoutAnEditionTheBaseDeviceStands(string title)
        => Assert.That(Synthetic.Match(title)?.Id, Is.EqualTo("switch-oled"));

    [TestCase("Steam Deck OLED 512GB weiß")]
    [TestCase("Steam Deck OLED keine Limited Edition")]
    public void SteamDeckWithoutEditionWordsIsTheBaseDevice(string title)
        => Assert.That(Synthetic.Match(title)?.Id, Is.EqualTo("deck-oled"));

    [TestCase("Nintendo Switch OLED Mario Red Edition Zelda Spiel", "nintendo-switch-oled-mario-red-edition")]
    [TestCase("Nintendo Switch OLED mit OVP Zelda Edition", "nintendo-switch-oled-zelda-edition")]
    [TestCase("Switch OLED 64GB mit Rechnung - Zelda TOTK Edition", "nintendo-switch-oled-zelda-edition")]
    [TestCase("Switch OLED weiß Zelda Tears of the Kingdom Spiel Day One Edition", "nintendo-switch-oled")]
    [TestCase("Switch Lite türkis Pokemon Schwert Zacian", "nintendo-switch-lite")]
    [TestCase("Nintendo Switch Lite Zacian und Zamazenta", "nintendo-switch-lite-zacian-zamazenta-edition")]
    [TestCase("Steam Deck OLED 512GB transparent", "valve-steam-deck-oled")]
    [TestCase("PS5 Konsole Spider-Man Miles Morales Edition", "sony-playstation-5")]
    [TestCase("Nintendo Switch Fortnite Wildcat Edition", "nintendo-switch")]
    [TestCase("Nintendo Switch Fortnite Special Edition", "nintendo-switch-fortnite-edition")]
    [TestCase("Valve Steam Deck OLED Limited Edition Smoke 1TB OVP", "valve-steam-deck-oled-limited-edition")]
    [TestCase("Steam Deck OLED Limited Edition 1TB White", "valve-steam-deck-oled-limited-edition-white")]
    [TestCase("Steam Deck OLED 1TB", "valve-steam-deck-oled")]
    [TestCase("Nintendo Switch OLED Modell Zelda Edition OVP", "nintendo-switch-oled-zelda-edition")]
    [TestCase("Nintendo Switch OLED weiß", "nintendo-switch-oled")]
    [TestCase("Nintendo Switch Animal Crossing Edition", "nintendo-switch-animal-crossing-edition")]
    [TestCase("Nintendo Switch V2 Fortnite Special Edition", "nintendo-switch-fortnite-edition")]
    [TestCase("Nintendo Switch V2", "nintendo-switch-v2")]
    [TestCase("PS5 Digital Edition 30th Anniversary", "sony-playstation-5-slim-digital-edition-30th-anniversary")]
    [TestCase("PS5 Digital Edition", "sony-playstation-5-digital-edition")]
    [TestCase("Xbox Series X 2TB Galaxy Black Special Edition", "microsoft-xbox-series-x-galaxy-black-special-edition")]
    [TestCase("Xbox Series X 2TB", "microsoft-xbox-series-x")]
    public void RealCatalogueEditions(string title, string expected)
        => Assert.That(Real.Match(title)?.Id, Is.EqualTo(expected));

    /// <summary>A spaced "+" before extras is "mit" for the accessory veto; a glued "+" and the word "Plus" still name a model, so a case for it stays a case.</summary>
    [TestCase("Nintendo Switch OLED + Zubehör", "nintendo-switch-oled")]
    [TestCase("Nintendo Switch OLED Zelda Edition + Zubehör", "nintendo-switch-oled-zelda-edition")]
    [TestCase("Steam Deck OLED Limited Edition 1TB + Tasche", "valve-steam-deck-oled-limited-edition")]
    [TestCase("PS5 Pro 30th Anniversary + Zubehör + OVP", "sony-playstation-5-pro-30th-anniversary")]
    [TestCase("Galaxy S21+ Hülle", null)]
    [TestCase("Samsung Galaxy S21+ Hülle schwarz", null)]
    [TestCase("iPhone 8 Plus Hülle", null)]
    [TestCase("Galaxy S21 Plus Case", null)]
    [TestCase("Hülle + Panzerglas iPhone 13", null)]
    [TestCase("Nintendo Switch + Konsole + OLED-Modell + Schwarz + Case + Rechnung", null)]
    public void SpacedPlusBeforeExtras(string title, string? expected)
        => Assert.That(Real.Match(title)?.Id, Is.EqualTo(expected));

    [TestCase("Xbox Series X mit Galaxy Black Controller", "microsoft-xbox-series-x")]
    [TestCase("Xbox Series X Galaxy Black Controller", "microsoft-xbox-series-x")]
    [TestCase("Xbox Series X 1TB Galaxy Black Controller dabei", "microsoft-xbox-series-x")]
    [TestCase("PS5 Slim + 30th Anniversary Controller", "sony-playstation-5-slim")]
    [TestCase("PS4 Pro + 500 Million Controller", "sony-playstation-4-pro")]
    [TestCase("Xbox Series X Galaxy Black Special Edition 2TB", "microsoft-xbox-series-x-galaxy-black-special-edition")]
    [TestCase("PS5 Pro 30th Anniversary mit Controller", "sony-playstation-5-pro-30th-anniversary")]
    public void APeripheralInAnEditionDesignIsNoEdition(string title, string expected)
        => Assert.That(Real.Match(title)?.Id, Is.EqualTo(expected));

    [TestCase("minecraft limited", true)]
    [TestCase("spiderman limited", true)]
    [TestCase("fortnite special", true)]
    [TestCase("xenoblade chronicles 2 limited", false)]
    [TestCase("limited edition 1tb weiss", false)]
    public void SeedEditionThemes(string words, bool expected)
        => Assert.That(KnownProductEditions.NamesSeedEditionTheme(words), Is.EqualTo(expected));

    [Test]
    public void EveryEditionNamesExistingBasesAndTerms()
    {
        var catalogue = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
        var ids = catalogue.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var edition in catalogue.Where(KnownProductEditions.IsEdition))
        {
            Assert.That(edition.PossibleAttributes[KnownProductEditions.EditionOfKey], Is.Not.Empty.And.All.Matches<string>(ids.Contains), edition.Id);
            Assert.That(edition.PossibleAttributes.GetValueOrDefault(KnownProductEditions.EditionTermsKey), Is.Not.Null.And.Not.Empty, edition.Id);
            Assert.That(edition.PossibleAttributes.ContainsKey("storage_size") || edition.PossibleAttributes.ContainsKey("color"), Is.False,
                $"{edition.Id}: an edition has one design and one storage, the listing's words must not split its page");
        }
    }
}
