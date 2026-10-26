using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class PowerToolCatalogTests
{
    [TestCase("MILWAUKEE M18 FSAGV125XB 18V FUEL 125mm WINKEL SCHLEIFER FLEX", "milwaukee-m18-fsagv125xb")]
    [TestCase("Festool CTLC-SYS 1 Akku Staubsauger", "festool-ctlc-sys-i")]
    [TestCase("Hilti SF 121-A Akku-Bohrschrauber mit 4 Akkus", "hilti-sf-121-a")]
    [TestCase("Festool CTLC SYS I-Basic", "festool-ctlc-sys-i")]
    [TestCase("Hilti SF121-A", "hilti-sf-121-a")]
    [TestCase("Festool CTLC SYS I mit Filtersack", "festool-ctlc-sys-i")]
    public void ExplicitWholeToolModelKeepsItsBodyIdentityWithoutInventingTheKit(string title, string id)
    {
        var match = ResaleCatalog.Match(title);
        Assert.That(match?.Id, Is.EqualTo(id));
        Assert.That(match!.PossibleAttributes["product_kind"], Is.EquivalentTo(new[] { "power_tool" }));
        Assert.That(match.PossibleAttributes["package_scope"], Is.EquivalentTo(new[] { "unspecified" }));
        Assert.That(match.PossibleAttributes.Keys, Does.Not.Contain("battery_count"));
        Assert.That(match.PossibleAttributes.Keys, Does.Not.Contain("manufacturer_article_number"));
    }

    [TestCase("Milwaukee M18 FSAGV125XPDB")]
    [TestCase("Milwaukee FSAGV125XB")]
    [TestCase("Festool CTL SYS")]
    [TestCase("Festool CTLC SYS II")]
    [TestCase("Hilti SF 120-A")]
    [TestCase("Hilti SF 121")]
    [TestCase("Stihl MS 661-C Griffheizung / 70cm Schwert")]
    [TestCase("Schleifscheiben für Milwaukee M18 FSAGV125XB")]
    [TestCase("Milwaukee M18 FSAGV125XB Schleifscheiben")]
    [TestCase("Filtersack für Festool CTLC SYS I")]
    [TestCase("Festool CTLC SYS I Filtersack")]
    [TestCase("Akku für Hilti SF 121-A")]
    [TestCase("Hilti SF 121-A Ladegerät")]
    [TestCase("Hilti SF 121-A und Milwaukee M18 FSAGV125XB")]
    [TestCase("Filtersack Festool CTLC SYS I")]
    [TestCase("Festool CTLC SYS I original Saugschlauch")]
    [TestCase("Schleifscheiben Milwaukee M18 FSAGV125XB")]
    [TestCase("Akku Staubsauger Filtersack Festool CTLC SYS I")]
    [TestCase("Filter Festool CTLC SYS I")]
    [TestCase("Filtersäcke Festool CTLC SYS I")]
    [TestCase("Schutzhauben Milwaukee M18 FSAGV125XB")]
    [TestCase("Saugschläuche Festool CTLC SYS I")]
    public void AdjacentModelsAccessoriesAndTheIncompleteStihlCodeDoNotClaimAWholeTool(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);
}
