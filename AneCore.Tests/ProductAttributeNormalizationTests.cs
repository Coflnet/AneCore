using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class ProductAttributeNormalizationTests
{
    [TestCase("38")]
    [TestCase("40")]
    [TestCase("42")]
    public void BareNumericGarmentSizesRemainSizes(string value)
    {
        Assert.That(Product.NormalizeAttributeClassification("size", value), Is.EqualTo(("size", value)));
    }

    [TestCase("128 GB", "storage", "128GB")]
    [TestCase("1TB", "storage", "1TB")]
    [TestCase("8GB", "ram", "8GB")]
    public void GenericSizesWithExplicitMemoryUnitsKeepTheirMeaning(string value, string key, string normalized)
    {
        Assert.That(Product.NormalizeAttributeClassification("size", value), Is.EqualTo((key, normalized)));
    }

    [TestCase("storage", "128", "storage", "128GB")]
    [TestCase("storage_size", "128 GB", "storage", "128GB")]
    [TestCase("ram", "8", "ram", "8GB")]
    [TestCase("ram_size", "8 GB", "ram", "8GB")]
    public void ExplicitMemoryKeysRetainNumericAndAliasNormalization(string key, string value, string normalizedKey, string normalizedValue)
    {
        Assert.That(Product.NormalizeAttributeClassification(key, value), Is.EqualTo((normalizedKey, normalizedValue)));
    }
}
