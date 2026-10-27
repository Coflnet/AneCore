using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class VinylOfferFormatStatementTests
{
    private static Product Offer() => new()
    {
        SeoId = "vinyl-offer-" + new string('a', 64) + "-used", Name = "Vinyl LP seller title",
        IdentifierType = "vinyl_offer", IdentifierValue = new string('a', 64), Condition = "used",
        Attributes = new() { ["product_kind"] = "vinyl_offer", ["custom"] = "keep" }
    };

    [Test]
    public void ConditionalWriteBindsObservedIdentityAndNameAndOnlySetsMissingFormatCell()
    {
        var product = Offer();
        var statement = ProductTableService.BuildVinylOfferFormatStatement(product, "LP", 123);
        Assert.That(statement.QueryString, Is.EqualTo("UPDATE products USING TTL 123 SET attributes['format'] = ? WHERE seo_id = ? IF name = ? AND identifier_type = ? AND identifier_value = ? AND canonical_seo_id = ? AND attributes['product_kind'] = ? AND attributes['format'] = null"));
        Assert.That(statement.QueryValues, Is.EqualTo(new object?[] {
            "LP", product.SeoId, product.Name, product.IdentifierType, product.IdentifierValue, null, "vinyl_offer" }));
        Assert.That(product.Attributes, Is.EquivalentTo(new Dictionary<string, string> { ["product_kind"] = "vinyl_offer", ["custom"] = "keep" }));
        Assert.That(statement.QueryString, Does.Not.Contain("SET attributes =").And.Not.Contain("last_updated ="));
    }

    [TestCase("format")]
    [TestCase("redirect")]
    [TestCase("type")]
    [TestCase("identifier")]
    [TestCase("kind")]
    [TestCase("invalid_format")]
    [TestCase("ttl")]
    public void StoreCannotTurnInvalidOrAlreadyPopulatedOfferIntoAnEnrichment(string change)
    {
        var product = Offer();
        switch (change)
        {
            case "format": product.Attributes!["format"] = ""; break;
            case "redirect": product.CanonicalSeoId = "another"; break;
            case "type": product.IdentifierType = "isbn13"; break;
            case "identifier": product.IdentifierValue = null; break;
            case "kind": product.Attributes!["product_kind"] = "hand_tool_offer"; break;
        }
        Assert.Throws<ArgumentException>(() => ProductTableService.BuildVinylOfferFormatStatement(product,
            change == "invalid_format" ? "Vinyl" : "LP", change == "ttl" ? 0 : 123));
    }
}
