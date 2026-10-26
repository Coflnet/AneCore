using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class ResaleCatalogTests
{
    [TestCase("Sony Cyber-shot DSC-H100 16,1 MP + Tasche & Stativ", "sony-cyber-shot-dsc-h100")]
    [TestCase("Sony DSC-H100 mit Tasche und Stativ", "sony-cyber-shot-dsc-h100")]
    [TestCase("Sony DSC-H100 with bag and tripod", "sony-cyber-shot-dsc-h100")]
    [TestCase("Sony DSC-H100 + Stativ", "sony-cyber-shot-dsc-h100")]
    [TestCase("Canon EOS 250D", "canon-eos-250d")]
    [TestCase("Krups Pro Aroma Filterkaffeemaschine F30908", "krups-pro-aroma-f30908")]
    [TestCase("Fahrrad Woom 2 Farbe Lemon", "woom-2-family")]
    [TestCase("Ravensburger GraviTrax Element Spiral Erweiterung", "ravensburger-gravitrax-spiral-22424")]
    [TestCase("PLAYMOBIL Feuerwehrstation 5361", "playmobil-5361")]
    [TestCase("Ravensburger Das verrückte Labyrinth 26955", "ravensburger-das-verruckte-labyrinth-26955")]
    [TestCase("Ravensburger Wer war's? 23494", "ravensburger-wer-war-s-23494")]
    [TestCase("Ravensburger Sagaland 26424", "ravensburger-sagaland-26424")]
    [TestCase("Ravensburger Sagaland Disney Edition 25121", "ravensburger-sagaland-disney-edition-25121")]
    [TestCase("Ravensburger Make 'n' Break 25174", "ravensburger-make-n-break-25174")]
    [TestCase("Ravensburger Die Maulwurf Company 26423", "ravensburger-die-maulwurf-company-26423")]
    [TestCase("Ravensburger Der zerstreute Pharao 26656", "ravensburger-der-zerstreute-pharao-26656")]
    [TestCase("Ravensburger Fang den Hut! 26736", "ravensburger-fang-den-hut-26736")]
    [TestCase("Ravensburger Wer stiehlt mir die Show - Das Spiel Buzzer-Edition 25177", "ravensburger-wer-stiehlt-mir-die-show-das-spiel-buzzer-edition-25177")]
    [TestCase("Ravensburger Collector's memory Schönste Reiseziele 27379", "ravensburger-collector-s-memory-schonste-reiseziele-27379")]
    [TestCase("Ravensburger Werwölfe Kids 22692", "ravensburger-werwolfe-kids-22692")]
    [TestCase("Ravensburger Collector's memory Walt Disney 27378", "ravensburger-collector-s-memory-walt-disney-27378")]
    [TestCase("Ravensburger Harry Potter Labyrinth 26031", "ravensburger-harry-potter-labyrinth-26031")]
    [TestCase("Ravensburger Sagaland Wintermärchen 22460", "ravensburger-sagaland-wintermarchen-22460")]
    [TestCase("Ravensburger Weltreise 26888", "ravensburger-weltreise-26888")]
    [TestCase("Ravensburger Pokémon Labyrinth 26949", "ravensburger-pokemon-labyrinth-26949")]
    [TestCase("Ravensburger memory Deutschland 20883", "ravensburger-memory-deutschland-20883")]
    [TestCase("Ravensburger Take it easy! 26738", "ravensburger-take-it-easy-26738")]
    [TestCase("Ravensburger Das Nilpferd in der Achterbahn 26772", "ravensburger-das-nilpferd-in-der-achterbahn-26772")]
    [TestCase("Ravensburger memory Natur 20881", "ravensburger-memory-natur-20881")]
    [TestCase("Ravensburger Junior Labyrinth 20847", "ravensburger-junior-labyrinth-20847")]
    [TestCase("Ravensburger Lotti Karotti 22343", "ravensburger-lotti-karotti-22343")]
    [TestCase("Ravensburger Schnappt Hubi! 24999", "ravensburger-schnappt-hubi-24999")]
    [TestCase("Ravensburger Plitsch-Platsch Pinguin 21325", "ravensburger-plitsch-platsch-pinguin-21325")]
    [TestCase("Ravensburger Max Mäuseschreck 24562", "ravensburger-max-mauseschreck-24562")]
    [TestCase("Ravensburger Scotland Yard 27515", "ravensburger-scotland-yard-27515")]
    [TestCase("Ravensburger Disney Villainous Einführung ins Böse 22687", "ravensburger-disney-villainous-einfuhrung-ins-bose-22687")]
    [TestCase("Ravensburger Carcassonne Labyrinth 25176", "ravensburger-carcassonne-labyrinth-25176")]
    [TestCase("Ravensburger Disney Labyrinth 27460", "ravensburger-disney-labyrinth-27460")]
    public void VerifiedResaleModelsHaveExactOrExplicitFamilyIdentity(string title, string id)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("PLAYMOBIL City Action Feuerwehrstation (5361) + 9463+ 71371")]
    [TestCase("GraviTrax Starterset und Element Spiral")]
    [TestCase("GraviTrax Spiral + Hammer")]
    [TestCase("Tasche für Sony DSC-H100")]
    [TestCase("Sony DSC-H100 Akku")]
    [TestCase("Schutzbleche für Woom 2")]
    [TestCase("Figuren für PLAYMOBIL 5361")]
    [TestCase("Stativ für Sony DSC-H100")]
    [TestCase("Tripod for Sony DSC-H100")]
    [TestCase("Restposten Sony DSC-H100")]
    [TestCase("Zahnrad Nikon Z50 Kamera")]
    [TestCase("Sony DSC-H100 Bedienungsanleitung")]
    [TestCase("Suche Sony DSC-H100")]
    [TestCase("Krups F30908 Reparaturservice")]
    [TestCase("Krups F30908 Glaskanne")]
    [TestCase("Krups F30908 zu vermieten")]
    [TestCase("Woom 2 Original")]
    [TestCase("Woom GO 2")]
    [TestCase("Original Woom 2")]
    [TestCase("Woom 2 Lemon Original")]
    [TestCase("Woom 2 und Woom 3")]
    [TestCase("Woom 2 Sattel")]
    [TestCase("PLAYMOBIL 5361 Anleitung")]
    [TestCase("Ravensburger Labyrinth")]
    [TestCase("Ravensburger Das verrückte Labyrinth")]
    [TestCase("Ravensburger Harry Potter Labyrinth English")]
    [TestCase("Ravensburger Sagaland Disney Edition")]
    [TestCase("Ravensburger 26031 und Ravensburger 26949")]
    [TestCase("Ravensburger 26031 Anleitung")]
    [TestCase("Ersatzteile für Ravensburger 26955")]
    public void NonOffersPartsAndAmbiguousBundlesNeverGetWholeProductIdentity(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);

    [TestCase("Digitalkameras", true)]
    [TestCase("Fahrräder", false)]
    [TestCase("Marketplace-only camera category", true)]
    public void ExistingKnownCategoryCompatibilityIsPreserved(string category, bool matches)
        => Assert.That(ResaleCatalog.Match("Sony DSC-H100", categoryPath: new[] { category }) != null, Is.EqualTo(matches));

    [Test]
    public void IncludedCameraExtrasDoNotMergeDifferentBodies()
    {
        Assert.That(ResaleCatalog.Match("Sony DSC-H100 mit Stativ")?.Id, Is.EqualTo("sony-cyber-shot-dsc-h100"));
        Assert.That(ResaleCatalog.Match("Sony DSC-H100 Canon EOS 250D"), Is.Null);
        Assert.That(ResaleCatalog.Match("Sony DSC-H100", "Teile fehlen"), Is.Null);
        Assert.That(ResaleCatalog.Match("Ravensburger GraviTrax Element Spiral", "Keine Anleitung vorhanden"), Is.Not.Null);
    }
}
