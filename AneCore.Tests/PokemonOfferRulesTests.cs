using Coflnet.Ane;
using NUnit.Framework;

namespace AneCore.Tests;

public class PokemonOfferRulesTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("unknown")]
    [TestCase("PSA10")]
    public void NameSetMechanicOrGradeCannotReplaceNumber(string? number)
    {
        var attrs = new Dictionary<string, string> { ["product_type"] = "single card", ["set_name"] = "Vivid Voltage", ["variant"] = "V", ["graded"] = "PSA 10" };
        if (number != null) attrs["card_number"] = number;
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle("Pokemon", attrs), Is.True);
    }

    [TestCase("043/185")]
    [TestCase("TG05/TG30")]
    [TestCase("SV49/SV94")]
    [TestCase("SWSH061")]
    [TestCase("XY110")]
    [TestCase("SVP001")]
    [TestCase("9")]
    public void ExistingNormalizedNumberShapesRemainIdentified(string number) =>
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle("Pokémon", new Dictionary<string, string>
            { ["product_type"] = "graded card", ["card_number"] = number }), Is.False);

    [TestCase("sealed product")]
    [TestCase("lot or bundle of cards")]
    [TestCase("accessory")]
    public void OtherTypedProductsAreOutsideTheSingleCardRule(string type) =>
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle("Pokemon", new Dictionary<string, string> { ["product_type"] = type }), Is.False);

    [Test]
    public void OtherBrandsAndUntypedMetadataAreNotReinterpreted()
    {
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle("Other", new Dictionary<string, string> { ["product_type"] = "single card" }), Is.False);
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle("Pokemon", new Dictionary<string, string>()), Is.False);
        Assert.That(PokemonOfferRules.IsUnidentifiedSingle(null, new Dictionary<string, string> { ["brand"] = " Pokemon ", ["product_type"] = "single card" }), Is.True);
    }
}
