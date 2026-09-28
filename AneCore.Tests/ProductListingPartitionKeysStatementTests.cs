using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class ProductListingPartitionKeysStatementTests
{
    [Test]
    public void Statement_ReadsOnePageOnly()
    {
        // Regression: with automatic paging the driver fetched every following page while the rows were
        // enumerated, so one "page" was the whole table and no paging state was ever returned.
        var statement = ProductTableService.BuildProductListingPartitionKeysStatement(500, null);

        Assert.That(statement.AutoPage, Is.False);
        Assert.That(statement.PageSize, Is.EqualTo(500));
        Assert.That(statement.PagingState, Is.Null);
    }

    [Test]
    public void Statement_ContinuesFromPagingState()
    {
        var pagingState = new byte[] { 1, 2, 3 };

        var statement = ProductTableService.BuildProductListingPartitionKeysStatement(50, pagingState);

        Assert.That(statement.AutoPage, Is.False);
        Assert.That(statement.PagingState, Is.EqualTo(pagingState));
    }
}
