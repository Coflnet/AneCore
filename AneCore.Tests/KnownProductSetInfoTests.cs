using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductSetInfoTests
{
    private static readonly IReadOnlyList<KnownProduct> Catalog = KnownProductSeed.LoadAll(includeExpandedCatalog: true);

    private static KnownProduct Set(string number) => Catalog.Single(p => p.Id == $"lego-{number}");

    [Test]
    public void SeedExposesTitleThemeYearAndPiecesForCargoTrain()
    {
        var info = Set("60198").SetInfo()!;
        Assert.That(info.Title, Is.EqualTo("Cargo Train"));
        Assert.That(info.Theme, Is.EqualTo("Trains"));
        Assert.That(info.ParentTheme, Is.EqualTo("City"));
        Assert.That(info.Year, Is.EqualTo(2018));
        Assert.That(info.Pieces, Is.EqualTo(1226));
    }

    [Test]
    public void LocalizedTermsCoverTheTrainWordInEveryLanguage()
    {
        var info = Set("60198").SetInfo()!;
        Assert.That(info.TermsFor("de"), Does.Contain("Zug"));
        Assert.That(info.TermsFor("fr"), Does.Contain("train"));
        Assert.That(info.TermsFor("nl"), Does.Contain("trein"));
        Assert.That(info.TermsFor("it"), Does.Contain("treno"));
        Assert.That(info.TermsFor("es"), Does.Contain("tren"));
        Assert.That(info.TermsFor("pl"), Does.Contain("pociąg"));
        Assert.That(info.SearchText(), Does.Contain("Cargo Train").And.Contain("Zug"));
    }

    [Test]
    public void SetNumberOutsideTheCatalogueIsResolvedByNumber()
    {
        Assert.That(Catalog.Any(p => p.Id == "lego-10874"), Is.False);
        var info = KnownProductSetInfo.ForSetNumber("10874")!;
        Assert.That(info.Title, Is.EqualTo("Steam Train"));
        Assert.That(info.ParentTheme, Is.EqualTo("Duplo"));
        Assert.That(info.TermsFor("de"), Does.Contain("Dampf").And.Contain("Zug"));
        Assert.That(KnownProductSetInfo.ForSetNumber("10874-1")?.Title, Is.EqualTo("Steam Train"));
        Assert.That(KnownProductSetInfo.ForSetNumber("99999999"), Is.Null);
    }

    [Test]
    public void FireStationIsNotTranslatedToBahnhof()
    {
        var terms = Catalog.Where(p => p.SetInfo()?.Title.Contains("Fire Station") == true).SelectMany(p => p.SetInfo()!.TermsFor("de")).ToList();
        Assert.That(terms, Is.Not.Empty);
        Assert.That(terms, Does.Not.Contain("Bahnhof"));
    }

    [Test]
    public void EnrichmentNeverChangesIdentityFieldsOrAliases()
    {
        var set = Set("60198");
        Assert.That(set.Name, Is.EqualTo("LEGO 60198"));
        Assert.That(set.Model, Is.EqualTo("60198"));
        Assert.That(set.Aliases, Does.Contain("LEGO 60198"));
        Assert.That(set.Aliases.Any(a => a.Contains("Zug") || a.Contains("treno")), Is.False);
        Assert.That(set.PossibleAttributes.Keys, Does.Not.Contain("set_name"));
    }

    [Test]
    public void MostSetsHaveATitleAndOnlyKnownSetsAreIndexed()
    {
        var sets = Catalog.Where(p => p.Vertical == "lego").ToList();
        Assert.That(sets.Count(p => p.SetInfo() != null), Is.GreaterThan(8000));
        Assert.That(KnownProductSetInfo.All.Count, Is.GreaterThan(sets.Count));
        Assert.That(new KnownProduct { Id = "apple-iphone-14" }.SetInfo(), Is.Null);
    }
}
