using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductReaderRecorderTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
    private static readonly KnownProductMatcher Matcher = new(Catalog);

    [TestCase("PocketBook 622 gebraucht", "pocketbook-622")]
    [TestCase("PocketBook Touch 622 E-Reader", "pocketbook-622")]
    [TestCase("PocketBook Touch Lux 623", "pocketbook-623")]
    [TestCase("PocketBook Touch Lux 2", "pocketbook-touch-lux-2")]
    [TestCase("PocketBook Touch Lux 3", "pocketbook-touch-lux-3")]
    [TestCase("PocketBook Basic Lux 2", "pocketbook-basic-lux-2")]
    [TestCase("PocketBook Basic Lux 3", "pocketbook-basic-lux-3")]
    [TestCase("Yamaha MDX595", "yamaha-mdx595")]
    [TestCase("Yamaha MDX-595", "yamaha-mdx595")]
    [TestCase("Yamaha Natural Sound Mini Disc Recorder MDX-595", "yamaha-mdx595")]
    [TestCase("LEGO 75305 Scout Trooper Helmet", "lego-75305")]
    public void ExactModels_ResolveWithoutExtractedPlatformAttributes(string title, string expected)
        => Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expected));

    [Test]
    public void ReviewedReaderAliases_AllResolveAndContainNoGuessedVariants()
    {
        var readers = Catalog.Where(p => p.Brand == "PocketBook").ToList();
        Assert.That(readers, Has.Count.EqualTo(30));
        foreach (var product in readers)
        {
            Assert.That(product.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "device_type" }));
            Assert.That(product.PossibleAttributes["device_type"], Is.EquivalentTo(new[] { "e-reader" }));
            foreach (var alias in product.Aliases)
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
        }
    }

    [Test]
    public void ReviewedTurntablesResolveEveryExactModelAndOnlyClaimInvariantDeviceType()
    {
        var turntables = Catalog.Where(p => p.PossibleAttributes.GetValueOrDefault("device_type")?.Contains("turntable") == true).ToList();
        Assert.That(turntables, Is.Not.Empty);
        foreach (var product in turntables)
        {
            Assert.That(product.PossibleAttributes.Keys, Is.EquivalentTo(new[] { "device_type" }));
            Assert.That(product.Categories, Is.EqualTo(new[] { "Elektronik", "Audio", "Audioplayer & -rekorder", "Plattenspieler" }));
            foreach (var alias in product.Aliases)
            {
                Assert.That(Matcher.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
                Assert.That(ResaleCatalog.Match(alias)?.Id, Is.EqualTo(product.Id), alias);
            }
        }
    }

    [TestCase("Dual CS418 Plattenspieler gebraucht", "dual-cs-418")]
    [TestCase("Dual CS 618 Q", "dual-cs-618q")]
    [TestCase("Thorens TD1600", "thorens-td-1600")]
    [TestCase("Thorens TD 402DD", "thorens-td-402-dd")]
    [TestCase("Technics SL1200 MK2 Plattenspieler", "technics-sl-1200mk2")]
    [TestCase("Technics SL 1200 MK5G", "technics-sl-1200mk5g")]
    [TestCase("Technics SL1210GR2", "technics-sl-1210gr2")]
    [TestCase("Dual CS 418 mit Ersatzriemen", "dual-cs-418")]
    [TestCase("Technics SL-1200MK2 mit Headshell", "technics-sl-1200mk2")]
    [TestCase("Thorens TD 201 mit neuer Nadel", "thorens-td-201")]
    [TestCase("Dual CS 418 mit Riemen", "dual-cs-418")]
    [TestCase("Dual CS 418 mit neuem Tonabnehmer", "dual-cs-418")]
    [TestCase("Thorens TD 201 with new cartridge", "thorens-td-201")]
    [TestCase("Thorens TD 201 with new stylus", "thorens-td-201")]
    [TestCase("Dual CS 418 with belt", "dual-cs-418")]
    [TestCase("Technics SL-1200MK2 mit Tonarm", "technics-sl-1200mk2")]
    [TestCase("Dual CS 418 mit Plattenteller", "dual-cs-418")]
    public void TurntablePunctuationAndGenerationsRemainExact(string title, string expected)
        => Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(expected));

    [TestCase("Dual Plattenspieler")]
    [TestCase("Thorens TD 9999")]
    [TestCase("Dual CS 4180")]
    [TestCase("Technics SL-1200 MK99")]
    [TestCase("Dual CS 418 und Dual CS 429")]
    [TestCase("Technics SL-1200MK2 Ersatzteil")]
    [TestCase("Riemen für Dual CS 418")]
    [TestCase("Nadel für Thorens TD 201")]
    [TestCase("Headshell for Technics SL-1200MK2")]
    public void UnknownAmbiguousAndExplicitPartTurntableOffersDoNotClaimACompleteDevice(string title)
        => Assert.That(Matcher.Match(title), Is.Null);

    [TestCase("Dual CS 418 Riemen")]
    [TestCase("Nadel Thorens TD 201")]
    [TestCase("Technics SL-1200MK2 Headshell")]
    [TestCase("Staubschutzhaube Technics SL-1200MK2")]
    [TestCase("Ersatzriemen Dual CS 418")]
    [TestCase("Thorens TD 201 Ersatznadel")]
    [TestCase("Tonabnehmer Dual CS 418")]
    [TestCase("Dual CS 418 Tonabnehmer")]
    [TestCase("Cartridge Thorens TD 201")]
    [TestCase("Thorens TD 201 cartridge")]
    [TestCase("Tonarm Technics SL-1200MK2")]
    [TestCase("Technics SL-1200MK2 Tonarm")]
    [TestCase("Plattenteller Dual CS 329")]
    [TestCase("Dual CS 329 Plattenteller")]
    [TestCase("Stylus Thorens TD 201")]
    [TestCase("Thorens TD 201 stylus")]
    [TestCase("Belt Dual CS 418")]
    [TestCase("Dual CS 418 belt")]
    public void TurntablePartNounsDoNotClaimTheCompleteDevice(string title)
        => Assert.That(Matcher.Match(title), Is.Null);

    [TestCase("PocketBook")]
    [TestCase("PocketBook E-Reader")]
    [TestCase("PocketBook Touch")]
    [TestCase("PocketBook Touch Lux")]
    [TestCase("PocketBook 626")]
    [TestCase("PocketBook 631")]
    [TestCase("PocketBook 840")]
    [TestCase("PocketBook 6220")]
    [TestCase("PocketBook Touch Lux 20")]
    [TestCase("PocketBook Touch Lux 3 Hülle")]
    [TestCase("Akku für PocketBook 622")]
    [TestCase("PocketBook Basic Lux 2 Ladekabel")]
    [TestCase("Yamaha MDX-595 Ersatzteil")]
    [TestCase("Yamaha MDX-596")]
    [TestCase("LEGO 75305 Bauanleitung")]
    [TestCase("LEGO 75305 nur Karton")]
    [TestCase("LEGO 75305 unvollständig")]
    [TestCase("LEGO 75305 Light Kit")]
    [TestCase("LEGO 75305 + 75304")]
    public void AmbiguousModelsAndScopedOffers_DoNotClaimTheDevice(string title)
        => Assert.That(Matcher.Match(title), Is.Null);

    [TestCase("PocketBook Touch 622")]
    [TestCase("Yamaha MDX-595")]
    [TestCase("Dual CS 418")]
    [TestCase("Thorens TD 201")]
    [TestCase("Technics SL-1200MK2")]
    public void IncompatibleKnownCategory_RejectsDevice(string title)
        => Assert.That(Matcher.Match(title, categoryPath: new[] { "Bekleidung & Accessoires", "Schuhe" }), Is.Null);
}
