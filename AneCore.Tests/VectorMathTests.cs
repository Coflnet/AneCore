using Coflnet.Ane.Opensearch;

namespace AneCore.Tests;

[TestFixture]
public class VectorMathTests
{
    [Test]
    public void IdenticalVectors_SimilarityIsOne()
    {
        float[] a = [1f, 2f, 3f];
        Assert.That(VectorMath.CosineSimilarity(a, a), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void OppositeVectors_SimilarityIsMinusOne()
    {
        float[] a = [1f, 0f];
        float[] b = [-1f, 0f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(-1.0).Within(1e-9));
    }

    [Test]
    public void OrthogonalVectors_SimilarityIsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(0.0).Within(1e-9));
    }

    [Test]
    public void ZeroVector_ReturnsZeroInsteadOfNaN()
    {
        float[] zero = [0f, 0f, 0f];
        float[] other = [1f, 2f, 3f];
        Assert.That(VectorMath.CosineSimilarity(zero, other), Is.EqualTo(0.0));
    }

    [Test]
    public void MismatchedLength_Throws()
    {
        float[] a = [1f, 2f];
        float[] b = [1f, 2f, 3f];
        Assert.Throws<ArgumentException>(() => VectorMath.CosineSimilarity(a, b));
    }

    [Test]
    public void ScaleInvariant_ParallelVectorsOfDifferentMagnitudeAreStillSimilarityOne()
    {
        float[] a = [2f, 0f, 0f];
        float[] b = [10f, 0f, 0f];
        Assert.That(VectorMath.CosineSimilarity(a, b), Is.EqualTo(1.0).Within(1e-9));
    }
}
