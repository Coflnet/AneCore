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
