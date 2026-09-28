using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class KnownProductCatalogMatcherTests
{
    [Test]
    public async Task Matcher_ReflectsSnapshotAfterRefresh()
    {
        var store = new InMemoryKnownProductStore();
        var catalog = new KnownProductCatalog(store);

        Assert.That(catalog.Matcher.Match("Apple iPhone 14"), Is.Null); // empty snapshot yet

        await store.UpsertAsync(new KnownProduct
        {
            Id = "apple-iphone-14",
            Brand = "Apple",
            Model = "iPhone 14",
            Name = "Apple iPhone 14",
            Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "iphone 14" },
        });
        await catalog.RefreshAsync();

        var result = catalog.Matcher.Match("Apple iPhone 14 128GB");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("apple-iphone-14"));
    }

    [Test]
    public async Task Matcher_IsSwappedAtomicallyOnRefresh_NotMutatedInPlace()
    {
        var store = new InMemoryKnownProductStore(new[]
        {
            new KnownProduct
            {
                Id = "apple-iphone-14",
                Brand = "Apple",
                Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "iphone 14" },
            },
        });
        var catalog = new KnownProductCatalog(store);
        await catalog.RefreshAsync();
        var firstMatcher = catalog.Matcher;

        await store.UpsertAsync(new KnownProduct
        {
            Id = "samsung-galaxy-s21",
            Brand = "Samsung",
            Aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "galaxy s21" },
        });
        await catalog.RefreshAsync();

        Assert.That(catalog.Matcher, Is.Not.SameAs(firstMatcher));
        Assert.That(firstMatcher.Match("Samsung Galaxy S21"), Is.Null, "old matcher instance must not see products added after it was built");
        Assert.That(catalog.Matcher.Match("Samsung Galaxy S21"), Is.Not.Null);
    }
}

[TestFixture]
public class KnownProductStoreSeedVersionTests
{
    [Test]
    public async Task InMemoryStore_AppliedSeedVersion_DefaultsNullThenPersists()
    {
        var store = new InMemoryKnownProductStore();
        Assert.That(await store.GetAppliedSeedVersionAsync(), Is.Null);

        await store.SetAppliedSeedVersionAsync("abc123");
        Assert.That(await store.GetAppliedSeedVersionAsync(), Is.EqualTo("abc123"));
    }
}
