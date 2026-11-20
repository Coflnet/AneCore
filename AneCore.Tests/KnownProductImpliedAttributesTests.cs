using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// <see cref="KnownProductImpliedAttributes"/>: a listing that omits a variant every unit of the catalogue product has gets that variant
/// ("Fire TV Stick 4K" is the 8 GB page), but only when the entry marks the key as implied and knows exactly one value.
/// </summary>
[TestFixture]
public class KnownProductImpliedAttributesTests
{
    private static readonly IReadOnlyList<KnownProduct> Seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);

    private static KnownProduct Entry(string id) => Seed.Single(p => p.Id == id);

    private static Dictionary<string, string> Attributes(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    [Test]
    public void FireTvStick4k_WithoutStorage_Gets8Gb()
    {
        var attributes = Attributes(("condition", "used"));

        var filled = KnownProductImpliedAttributes.Fill(Entry("amazon-fire-tv-stick-4k"), attributes);

        Assert.That(attributes["storage_size"], Is.EqualTo("8GB"));
        Assert.That(filled, Is.EqualTo(new[] { ("storage_size", "8GB") }));
    }

    [Test]
    public void StatedStorage_IsKept()
    {
        var attributes = Attributes(("storage_size", "8 GB"));

        var filled = KnownProductImpliedAttributes.Fill(Entry("amazon-fire-tv-stick-4k"), attributes);

        Assert.That(attributes["storage_size"], Is.EqualTo("8 GB"));
        Assert.That(filled, Is.Empty);
    }

    [Test]
    public void ProductWithSeveralStorageSizes_IsNotFilled()
    {
        var attributes = Attributes();

        KnownProductImpliedAttributes.Fill(Entry("apple-iphone-15"), attributes);

        Assert.That(attributes.ContainsKey("storage_size"), Is.False);
    }

    [Test]
    public void SingleImportedValueWithoutMarker_IsNotFilled()
    {
        // the import lists one storage size for the Galaxy A52s (it is sold with 128 and 256 GB): no marker, no fill
        var product = Entry("samsung-galaxy-a52s");
        Assert.That(product.PossibleAttributes["storage_size"], Has.Count.EqualTo(1));
        var attributes = Attributes();

        KnownProductImpliedAttributes.Fill(product, attributes);

        Assert.That(attributes, Is.Empty);
    }

    [Test]
    public void MarkerOnAKeyWithTwoValues_IsNotFilled()
    {
        var product = new KnownProduct
        {
            Id = "amazon-fire-tv-stick-4k-max",
            PossibleAttributes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["storage_size"] = new(StringComparer.OrdinalIgnoreCase) { "8GB", "16GB" },
                [KnownProductImpliedAttributes.ImpliedAttributesKey] = new(StringComparer.OrdinalIgnoreCase) { "storage_size" },
            },
        };
        var attributes = Attributes();

        KnownProductImpliedAttributes.Fill(product, attributes);

        Assert.That(attributes.ContainsKey("storage_size"), Is.False);
    }

    [Test]
    public void FireTvStick4kMax_IsNotCatalogued_SoNoGenerationIsGuessed()
    {
        Assert.That(Seed.Any(p => p.Id == "amazon-fire-tv-stick-4k-max"), Is.False);
        var match = new KnownProductMatcher(Seed).Match("Amazon Fire TV Stick 4K Max", null, null, null);
        Assert.That(match?.Id, Is.Not.EqualTo("amazon-fire-tv-stick-4k").And.Not.EqualTo("amazon-fire-tv-stick"));
    }

    [TestCase("Amazon Fire TV Stick 4K", "amazon-fire-tv-stick-4k")]
    [TestCase("Fire TV Stick 4K Select 8GB", "amazon-fire-tv-stick-4k-select")]
    [TestCase("Amazon Fire TV Stick 4K Plus", "amazon-fire-tv-stick-4k-plus")]
    [TestCase("Amazon Fire TV Stick mit Alexa Sprachfernbedienung", "amazon-fire-tv-stick")]
    [TestCase("Fire TV Stick Lite", "amazon-fire-tv-stick-lite")]
    [TestCase("Amazon Fire TV Cube", "amazon-fire-tv-cube")]
    public void FireTvTitles_MatchTheirOwnEntry(string title, string expected)
    {
        Assert.That(new KnownProductMatcher(Seed).Match(title, null, null, null)?.Id, Is.EqualTo(expected));
    }

    [TestCase("Amazon Fire TV Stick 3. Generation")]
    [TestCase("Amazon Fire TV Stick 4K 2. Gen")]
    public void GenerationTitles_KeepTheirExtractorPage(string title)
    {
        Assert.That(new KnownProductMatcher(Seed).Match(title, null, null, null)?.Id, Is.Null.Or.Not.StartWith("amazon-fire-tv"));
    }
}
