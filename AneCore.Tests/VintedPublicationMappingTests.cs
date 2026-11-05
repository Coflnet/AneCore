using Coflnet.Ane;

namespace AneCore.Tests;

public class VintedPublicationMappingTests
{
    [TestCase("2312", "books")]
    [TestCase("2319", "books")]
    [TestCase("2320", "books")]
    [TestCase("2318", "books")]
    [TestCase("2363", "books")]
    [TestCase("2364", "books")]
    [TestCase("2365", "books")]
    [TestCase("5426", "books")]
    [TestCase("5427", "books")]
    [TestCase("5425", "comics")]
    public void NativeLeafAndFallbackStreamIdsKeepTheirPublicationScope(string id, string vertical)
    {
        var scope = CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Vinted, id);
        Assert.That(scope?.Vertical?.Key, Is.EqualTo(vertical));
        Assert.That(scope!.Vertical!.IsActive, Is.True);
    }

    [TestCase("2309")]
    [TestCase("5424")]
    [TestCase("3036")]
    [TestCase("3038")]
    [TestCase("3039")]
    [TestCase("3040")]
    [TestCase("3037")]
    public void BroadMediaAndNonBookSiblingLeavesDoNotGainPublicationScope(string id) =>
        Assert.That(CategoryScopeCatalog.Shared.ResolvePlatform(Platform.Vinted, id)?.Vertical?.Key,
            Is.Not.EqualTo("books").And.Not.EqualTo("comics"));
}
