// ReSharper disable once CheckNamespace
namespace Coflnet.Ane.Opensearch;

/// <summary>Small vector-math helpers shared by the clothing visual reference stores.</summary>
public static class VectorMath
{
    /// <summary>
    /// Cosine similarity of two equal-length vectors, in [-1, 1] (1 = identical direction). Used to
    /// recompute exact similarity on top of the approximate kNN ranking OpenSearch returns, and by
    /// <see cref="InMemoryVisualReferenceStore"/> for its brute-force search.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length != b.Length)
            throw new ArgumentException($"Vectors must have the same length ({a.Length} vs {b.Length}).", nameof(b));

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }
        if (normA == 0 || normB == 0)
            return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
