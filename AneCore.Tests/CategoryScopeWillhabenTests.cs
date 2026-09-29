using Coflnet.Ane;

namespace AneCore.Tests;

[TestFixture]
public class CategoryScopeWillhabenTests
{
    private static readonly CategoryScopeCatalog Scope = CategoryScopeCatalog.Shared;

    [Test]
    public void IdPath_ResolvesMostSpecificIdFirst()
    {
        // Regression: Willhaben delivers an id path; the out-of-scope root 6462 (Freizeit) must not hide the
        // in-scope leaf 6658 (Sammelkarten).
        var scope = Scope.ResolvePlatform(Platform.Willhaben, "6462;6621;6658;6662");

        Assert.That(scope, Is.Not.Null);
        Assert.That(scope!.IsOutOfScope, Is.False);
        Assert.That(scope.Vertical, Is.EqualTo(Scope.ResolvePlatform(Platform.Willhaben, "6658")!.Vertical));
    }

    [Test]
    public void IdPath_FallsBackToRoot()
    {
        var scope = Scope.ResolvePlatform(Platform.Willhaben, "4390;4598;4599;4601");

        Assert.That(scope, Is.Not.Null);
        Assert.That(scope!.IsOutOfScope, Is.True);
    }

    [Test]
    public void IdPath_UnknownIds_AreNotResolved()
    {
        Assert.That(Scope.ResolvePlatform(Platform.Willhaben, "999001;999002"), Is.Null);
    }

    [Test]
    public void SingleId_StillResolves()
    {
        var scope = Scope.ResolvePlatform(Platform.Willhaben, "2691");

        Assert.That(scope, Is.Not.Null);
        Assert.That(scope!.IsOutOfScope, Is.False);
    }
}
