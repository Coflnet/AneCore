using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class RegroupMoveSchemaTests
{
    [Test]
    public void ColumnsAddedAfterFirstRelease_AreEnsuredOnExistingTables()
    {
        // an existing production regroup_moves table is not altered by CreateIfNotExists: every column added later must be in this list
        Assert.That(ProductTableService.RegroupMoveAddedColumns, Does.Contain(("evidence", "text")));
        Assert.That(ProductTableService.RegroupMoveAddedColumns, Does.Contain(("detach_reason", "text")));
    }

    [Test]
    public void Evidence_IsOptionalText()
    {
        var move = new RegroupMove();
        Assert.That(move.Evidence, Is.Null);
        Assert.That(typeof(RegroupMove).GetProperty(nameof(RegroupMove.Evidence))!.PropertyType, Is.EqualTo(typeof(string)));
    }
}
