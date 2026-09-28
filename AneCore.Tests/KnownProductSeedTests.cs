using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductSeedTests
{
    [Test]
    public void LoadAll_FindsEmbeddedSeedResources()
    {
        var seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
        Assert.That(seed, Is.Not.Empty);
    }

    [Test]
    public void Version_IsNonEmptyAndStableAcrossCalls()
    {
        var first = KnownProductSeed.Version;
        var second = KnownProductSeed.Version;
        Assert.That(first, Is.Not.Empty);
        Assert.That(second, Is.SameAs(first));
    }
}
