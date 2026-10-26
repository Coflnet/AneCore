using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

public class HandToolBundleTests
{
    [TestCase("KNIPEX 8701250 8603180")]
    [TestCase("KNIPEX 87 01 250 86 03 180")]
    [TestCase("KNIPEX 87 01 250 / 86 03 180")]
    public void TwoKnownArticlesWithOneManufacturerAreNotOneTool(string title)
        => Assert.That(ResaleCatalog.Match(title), Is.Null);

    [TestCase("KNIPEX 87 01 250 Wasserpumpenzange 250 mm")]
    [TestCase("KNIPEX 87 01 250 8701250")]
    public void DimensionsOrRepeatedSameArticleDoNotCreateAnExtraTool(string title)
        => Assert.That(ResaleCatalog.Match(title)?.Id, Is.EqualTo("knipex-8701250"));
}
