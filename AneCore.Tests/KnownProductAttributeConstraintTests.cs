using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// Regression tests for <see cref="KnownProductAttributeConstraint"/>'s twin-key handling - AneNotifier's
/// extractor writes a raw plain-number key (<c>storage_gb</c>/<c>ram_gb</c>/<c>screen_size_inch</c>)
/// alongside the formatted canonical one (<c>storage_size</c>/<c>ram_size</c>/<c>screen_size</c>) into the
/// same attributes dictionary (see <c>Extraction/RuleBasedExtractionService.ToProductInfo</c>, read-only
/// reference in AneNotifier). <see cref="KnownProductAttributeConstraint.Apply"/> must keep both in sync.
/// </summary>
[TestFixture]
public class KnownProductAttributeConstraintTests
{
    private static KnownProduct IPhone15ProMax() => new()
    {
        Id = "apple-iphone-15-pro-max",
        Brand = "Apple",
        Model = "iPhone 15 Pro Max",
        Name = "Apple iPhone 15 Pro Max",
        PossibleAttributes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage_size"] = new(StringComparer.OrdinalIgnoreCase) { "256GB", "512GB", "1TB" },
            ["color"] = new(StringComparer.OrdinalIgnoreCase), // free value
        },
    };

    private static KnownProduct MacBookPro14() => new()
    {
        Id = "apple-macbook-pro-14",
        Brand = "Apple",
        Model = "MacBook Pro 14-inch",
        Name = "Apple MacBook Pro 14-inch",
        PossibleAttributes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage_size"] = new(StringComparer.OrdinalIgnoreCase) { "512GB", "1TB" },
            ["ram_size"] = new(StringComparer.OrdinalIgnoreCase) { "16GB", "32GB" },
            ["screen_size"] = new(StringComparer.OrdinalIgnoreCase) { "14\"" },
        },
    };

    [Test]
    public void DroppingCanonicalStorageSize_AlsoDropsStorageGbTwin()
    {
        // Regression: live product apple-iphone-15-pro-max-silver-defekt-used kept storage_gb="265"
        // after storage_size was correctly dropped for not being a verified storage tier.
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage_size"] = "265GB",
            ["storage_gb"] = "265",
        };

        var dropped = KnownProductAttributeConstraint.Apply(IPhone15ProMax(), attributes);

        Assert.That(attributes.ContainsKey("storage_size"), Is.False);
        Assert.That(attributes.ContainsKey("storage_gb"), Is.False);
        Assert.That(dropped.Select(d => d.Key), Is.EquivalentTo(new[] { "storage_size", "storage_gb" }));
    }

    [Test]
    public void ValidCanonicalStorageSize_KeepsBothTwins()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage_size"] = "256GB",
            ["storage_gb"] = "256",
        };

        var dropped = KnownProductAttributeConstraint.Apply(IPhone15ProMax(), attributes);

        Assert.That(dropped, Is.Empty);
        Assert.That(attributes["storage_size"], Is.EqualTo("256GB"));
        Assert.That(attributes["storage_gb"], Is.EqualTo("256"));
    }

    [Test]
    public void RamGbTwin_WithoutCanonicalRamSize_ValidatedIndependently()
    {
        // The canonical key was never derived (e.g. a caller that only ever writes the raw extractor
        // keys) - the twin must still be checked against the product's verified values.
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ram_gb"] = "999",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(attributes.ContainsKey("ram_gb"), Is.False);
        Assert.That(dropped.Single().Key, Is.EqualTo("ram_gb"));
    }

    [Test]
    public void ValidRamGbTwin_WithoutCanonicalRamSize_IsKept()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ram_gb"] = "16",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(dropped, Is.Empty);
        Assert.That(attributes["ram_gb"], Is.EqualTo("16"));
    }

    [Test]
    public void ScreenSizeInchTwin_ComparesAgainstQuoteSuffixedCanonicalShape()
    {
        // screen_size_inch is a bare number ("14") while the catalogue's screen_size values carry a
        // trailing inch mark ("14\"") - the twin must be reshaped before comparing, not compared raw
        // (which would otherwise always mismatch and drop every valid screen size).
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["screen_size_inch"] = "14",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(dropped, Is.Empty);
        Assert.That(attributes["screen_size_inch"], Is.EqualTo("14"));
    }

    [Test]
    public void ScreenSizeInchTwin_InvalidSize_IsDropped()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["screen_size_inch"] = "17",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(attributes.ContainsKey("screen_size_inch"), Is.False);
        Assert.That(dropped.Single().Key, Is.EqualTo("screen_size_inch"));
    }

    [Test]
    public void DroppingCanonicalScreenSize_AlsoDropsScreenSizeInchTwin()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["screen_size"] = "17\"",
            ["screen_size_inch"] = "17",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(attributes.ContainsKey("screen_size"), Is.False);
        Assert.That(attributes.ContainsKey("screen_size_inch"), Is.False);
        Assert.That(dropped.Select(d => d.Key), Is.EquivalentTo(new[] { "screen_size", "screen_size_inch" }));
    }

    [Test]
    public void GpuVramGb_HasNoCanonicalTwin_IsNeverTouched()
    {
        // gpu_vram_gb is the one other raw key the extractor writes, but AneNotifier's
        // RuleBasedExtractionService never derives a canonical twin for it - Apply must leave it alone
        // regardless of the product's PossibleAttributes (nothing declares "gpu_vram_gb" as a
        // slug-differentiating key).
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpu_vram_gb"] = "12",
        };

        var dropped = KnownProductAttributeConstraint.Apply(MacBookPro14(), attributes);

        Assert.That(dropped, Is.Empty);
        Assert.That(attributes["gpu_vram_gb"], Is.EqualTo("12"));
    }
}
