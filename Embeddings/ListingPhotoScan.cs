using Cassandra;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.Embeddings;

/// <summary>First photo (image index 0) of an embedded clothing listing; no embedding blob.</summary>
public sealed record ListingPhotoRef(string ListingId, Platform Platform, string ImageUrl, string GroupKey, DateTime CreatedAt);

/// <summary>One page of a scan; <see cref="NextCursor"/> is null when the end of the table was reached.</summary>
public sealed record ListingPhotoScanPage(IReadOnlyList<ListingPhotoRef> Photos, byte[]? NextCursor);

/// <summary>Cursor scan over the first photos of embedded listings, for jobs that walk the table slowly.</summary>
public interface IListingPhotoScan
{
    /// <summary>Reads about <paramref name="pageSize"/> rows starting at <paramref name="cursor"/> (null starts at the beginning) and returns their image index 0 rows.</summary>
    Task<ListingPhotoScanPage> ScanFirstPhotosAsync(int pageSize, byte[]? cursor);
}

public static class ListingPhotoScanStatements
{
    /// <summary>No embedding column, automatic paging off (a page is one page), platform read as int.</summary>
    public static IStatement Build(int pageSize, byte[]? cursor)
    {
        var statement = new SimpleStatement(
                "SELECT listing_id, platform, image_index, image_url, group_key, created_at FROM listing_image_embeddings")
            .SetPageSize(pageSize)
            .SetAutoPage(false);
        if (cursor != null)
            statement = statement.SetPagingState(cursor);
        return statement;
    }
}

public partial class CassandraListingEmbeddingStore : IListingPhotoScan
{
    public async Task<ListingPhotoScanPage> ScanFirstPhotosAsync(int pageSize, byte[]? cursor)
    {
        var rows = await session.ExecuteAsync(ListingPhotoScanStatements.Build(pageSize, cursor));
        var photos = new List<ListingPhotoRef>();
        foreach (var row in rows)
        {
            if (row.GetValue<int>("image_index") != 0)
                continue;
            photos.Add(new ListingPhotoRef(row.GetValue<string>("listing_id"), (Platform)row.GetValue<int>("platform"),
                row.GetValue<string>("image_url") ?? "", row.GetValue<string>("group_key") ?? "",
                row.GetValue<DateTime>("created_at")));
        }
        return new ListingPhotoScanPage(photos, rows.PagingState);
    }
}

public partial class InMemoryListingEmbeddingStore : IListingPhotoScan
{
    /// <summary>Cursor is the 4 byte big endian offset into the listings ordered by their key.</summary>
    public Task<ListingPhotoScanPage> ScanFirstPhotosAsync(int pageSize, byte[]? cursor)
    {
        var offset = cursor is { Length: 4 } ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(cursor) : 0;
        var listings = byListing.OrderBy(kv => kv.Key.Platform).ThenBy(kv => kv.Key.ListingId, StringComparer.Ordinal).ToList();
        var page = listings.Skip(offset).Take(Math.Max(1, pageSize)).ToList();
        var photos = page.Select(kv => kv.Value.TryGetValue(0, out var e) ? e : null).Where(e => e != null)
            .Select(e => new ListingPhotoRef(e!.ListingId, e.Platform, e.ImageUrl, e.GroupKey, e.CreatedAt)).ToList();
        var next = offset + page.Count;
        byte[]? nextCursor = null;
        if (next < listings.Count)
        {
            nextCursor = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(nextCursor, next);
        }
        return Task.FromResult(new ListingPhotoScanPage(photos, nextCursor));
    }
}
