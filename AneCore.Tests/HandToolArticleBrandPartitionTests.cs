using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

[TestFixture]
public class HandToolArticleBrandPartitionTests
{
    private static KnownProduct Tool(string id, string brand, string article) => new()
    {
        Id = id, Brand = brand, Name = id, Model = article,
        Aliases = new(StringComparer.OrdinalIgnoreCase) { "unrelated alias " + id },
        PossibleAttributes = new() { ["product_kind"] = ["hand_tool"], ["article_number"] = [article] }
    };

    private static readonly KnownProductMatcher Matcher = new([
        Tool("alpha", "Alpha Tools", "A-100"), Tool("beta", "Beta", "B-200")]);

    [TestCase("Alpha Tools Elektronikzange A-100", "alpha")]
    [TestCase("Beta pince B-200", "beta")]
    public void DescriptorSeparatedArticlesRetainTheirNormalizedBrandIdentity(string title, string id) =>
        Assert.That(Matcher.Match(title)?.Id, Is.EqualTo(id));

    [TestCase("unbranded Elektronikzange A-100")]
    [TestCase("Beta Elektronikzange A-100")]
    [TestCase("Alpha Tools Beta Elektronikzange A-100")]
    [TestCase("Alpha Tools Elektronikzange A-100/1")]
    public void MissingConflictingBrandAndAttachedVariantCannotChooseAnArticle(string title) =>
        Assert.That(Matcher.Match(title), Is.Null);

    [Test]
    public void DuplicateExactArticleStillCannotChooseOneProduct()
    {
        var duplicate = new KnownProductMatcher([Tool("one", "Alpha Tools", "A-100"), Tool("two", "Alpha Tools", "A-100")]);
        Assert.That(duplicate.Match("Alpha Tools Elektronikzange A-100"), Is.Null);
    }
}
