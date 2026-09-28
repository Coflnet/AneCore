using Coflnet.Ane.Embeddings;

namespace AneCore.Tests;

[TestFixture]
public class EmbeddingBlobCodecTests
{
    private static float[] SampleVector(float seed = 0f)
    {
        var vector = new float[EmbeddingBlobCodec.Dimensions];
        for (var i = 0; i < vector.Length; i++)
            vector[i] = seed + i * 0.001f;
        return vector;
    }

    [Test]
    public void RoundTrip_PreservesValues()
    {
        var original = SampleVector(-3.5f);

        var blob = EmbeddingBlobCodec.Encode(original);
        var decoded = EmbeddingBlobCodec.Decode(blob);

        Assert.That(blob.Length, Is.EqualTo(EmbeddingBlobCodec.ByteLength));
        Assert.That(decoded, Is.EqualTo(original));
    }

    [Test]
    public void Encode_ByteLength_Is2048()
    {
        var blob = EmbeddingBlobCodec.Encode(SampleVector());
        Assert.That(blob.Length, Is.EqualTo(2048));
    }

    [Test]
    public void Encode_IsLittleEndian()
    {
        var vector = new float[EmbeddingBlobCodec.Dimensions];
        vector[0] = 1.0f; // 0x3F800000

        var blob = EmbeddingBlobCodec.Encode(vector);

        Assert.That(blob[0], Is.EqualTo(0x00));
        Assert.That(blob[1], Is.EqualTo(0x00));
        Assert.That(blob[2], Is.EqualTo(0x80));
        Assert.That(blob[3], Is.EqualTo(0x3F));
    }

    [Test]
    public void Encode_WrongLength_Throws()
    {
        var tooShort = new float[EmbeddingBlobCodec.Dimensions - 1];
        Assert.Throws<ArgumentException>(() => EmbeddingBlobCodec.Encode(tooShort));
    }

    [Test]
    public void Decode_WrongLength_Throws()
    {
        var tooLong = new byte[EmbeddingBlobCodec.ByteLength + 4];
        Assert.Throws<ArgumentException>(() => EmbeddingBlobCodec.Decode(tooLong));
    }

    [Test]
    public void Decode_EmptyBlob_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmbeddingBlobCodec.Decode([]));
    }
}
