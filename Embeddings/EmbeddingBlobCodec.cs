using System.Buffers.Binary;

namespace Coflnet.Ane.Embeddings;

/// <summary>
/// Converts a fashion-clip embedding vector to and from the fixed-width blob stored in
/// <see cref="ListingImageEmbedding.Embedding"/>: <see cref="Dimensions"/> little-endian float32 values
/// back to back, regardless of the host machine's endianness.
/// </summary>
public static class EmbeddingBlobCodec
{
    /// <summary>fashion-clip embedding size.</summary>
    public const int Dimensions = 512;

    /// <summary>512 float32 values * 4 bytes = 2048 bytes.</summary>
    public const int ByteLength = Dimensions * sizeof(float);

    /// <summary>Encodes a <see cref="Dimensions"/>-length vector to its little-endian blob form.</summary>
    /// <exception cref="ArgumentException"><paramref name="embedding"/> does not have exactly <see cref="Dimensions"/> values.</exception>
    public static byte[] Encode(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != Dimensions)
            throw new ArgumentException(
                $"Embedding must have exactly {Dimensions} dimensions, got {embedding.Length}.", nameof(embedding));

        var blob = new byte[ByteLength];
        for (var i = 0; i < Dimensions; i++)
            BinaryPrimitives.WriteSingleLittleEndian(blob.AsSpan(i * sizeof(float), sizeof(float)), embedding[i]);
        return blob;
    }

    /// <summary>Decodes a blob previously produced by <see cref="Encode"/> back into a <see cref="Dimensions"/>-length vector.</summary>
    /// <exception cref="ArgumentException"><paramref name="blob"/> is not exactly <see cref="ByteLength"/> bytes long.</exception>
    public static float[] Decode(byte[] blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length != ByteLength)
            throw new ArgumentException(
                $"Embedding blob must be exactly {ByteLength} bytes, got {blob.Length}.", nameof(blob));

        var embedding = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
            embedding[i] = BinaryPrimitives.ReadSingleLittleEndian(blob.AsSpan(i * sizeof(float), sizeof(float)));
        return embedding;
    }
}
