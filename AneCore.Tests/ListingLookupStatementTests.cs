using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class ListingLookupStatementTests
{
    [Test]
    public void Statement_BindsNoEnum()
    {
        // Regression: binding the Platform enum threw "Unknown Cassandra target type for CLR type
        // Coflnet.Ane.Platform" on every listing lookup.
        var cql = ProductTableService.BuildListingLookupCql("10180761719");

        Assert.That(cql.Arguments, Is.Not.Empty);
        Assert.That(cql.Arguments.Select(a => a!.GetType().IsEnum), Has.All.False);
        Assert.That(cql.Statement, Does.Contain("id = ?").And.Not.Contain("platform"));
    }

    [Test]
    public void SelectListingForPlatform_PicksMatchingPlatform()
    {
        var rows = new List<Listing>
        {
            new() { Id = "1", Platform = Platform.Ebay },
            new() { Id = "1", Platform = Platform.Kleinanzeigen },
        };

        Assert.That(ProductTableService.SelectListingForPlatform(rows, Platform.Kleinanzeigen)!.Platform, Is.EqualTo(Platform.Kleinanzeigen));
        Assert.That(ProductTableService.SelectListingForPlatform(rows, Platform.Vinted), Is.Null);
    }
}
