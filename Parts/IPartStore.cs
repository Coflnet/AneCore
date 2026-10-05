using System.Collections.Concurrent;

namespace Coflnet.Ane.Parts;

/// <summary>
/// Persistence of the mechanical part index: the state of a page, its measurements, its searchable signature, its part numbers, what it fits and the same-part edges.
/// Implemented by Cassandra (<see cref="CassandraPartStore"/>, every row with a TTL that is renewed on write) and in memory (<see cref="InMemoryPartStore"/>, tests). Every write is an upsert.
/// <see cref="InitializeAsync"/> creates the tables and is called once at service startup, never from the grouping path.
/// </summary>
public interface IPartStore
{
    /// <summary>Creates the tables when needed; safe to call repeatedly.</summary>
    Task InitializeAsync();

    Task<PartPageState?> GetPageStateAsync(string seoId);
    Task UpsertPageStateAsync(PartPageState state);
    Task DeletePageStateAsync(string seoId);

    Task UpsertMeasurementsAsync(IEnumerable<PartMeasurement> measurements);
    /// <summary>Deletes the rows of the given attribute keys of a page (a value that is no longer known).</summary>
    Task DeleteMeasurementsAsync(string seoId, IEnumerable<string> attributeKeys);
    /// <summary>Deletes every measurement row of a page.</summary>
    Task DeleteMeasurementsAsync(string seoId);
    /// <summary>Deletes one row (attribute key, source, listing key) of a page.</summary>
    Task DeleteMeasurementRowsAsync(string seoId, string attributeKey, string source, string listingKey);
    Task<IReadOnlyList<PartMeasurement>> GetMeasurementsAsync(string seoId);

    Task UpsertSignatureAsync(PartSignatureRow row);
    Task DeleteSignatureAsync(string partClass, string partKey, string seoId);
    /// <summary>The pages of one class with one primary key value (<see cref="PartSignatureRow.UnknownKey"/> for the pages without one), at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<PartSignatureRow>> GetBySignatureAsync(string partClass, string partKey, int limit);

    Task UpsertPartNumberAsync(PartNumberRow row);
    Task DeletePartNumberAsync(string partNumberKey, string seoId);
    Task<IReadOnlyList<PartNumberRow>> GetByPartNumberAsync(string partNumberKey, int limit);

    /// <summary>Writes the fit into both the by-machine and the by-part table.</summary>
    Task UpsertFitAsync(PartFitRow fit);
    Task DeleteFitAsync(string machineKey, string seoId);
    Task<IReadOnlyList<PartFitRow>> GetPartsForMachineAsync(string machineKey, int limit);
    Task<IReadOnlyList<PartFitRow>> GetMachinesForPartAsync(string seoId);

    /// <summary>Writes the edge in both directions.</summary>
    Task UpsertSameEdgeAsync(PartSameEdge edge);
    /// <summary>Deletes the edge in both directions.</summary>
    Task DeleteSameEdgeAsync(string seoId, string otherSeoId);
    Task<IReadOnlyList<PartSameEdge>> GetSameEdgesAsync(string seoId, int limit = MaxEdges);

    const int MaxEdges = 100;
}

/// <summary>In-memory <see cref="IPartStore"/> for tests. Thread-safe, no persistence.</summary>
public sealed class InMemoryPartStore : IPartStore
{
    private readonly ConcurrentDictionary<string, PartPageState> pages = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<(string Key, string Source, string Listing), PartMeasurement>> measurements = new();
    private readonly ConcurrentDictionary<(string Class, string Key), ConcurrentDictionary<string, PartSignatureRow>> signatures = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PartNumberRow>> partNumbers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PartFitRow>> fitsByMachine = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PartFitRow>> fitsByPart = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PartSameEdge>> edges = new();

    public int Writes;
    public int Deletes;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<PartPageState?> GetPageStateAsync(string seoId) => Task.FromResult(pages.TryGetValue(seoId, out var s) ? s : null);

    public Task UpsertPageStateAsync(PartPageState state)
    {
        Interlocked.Increment(ref Writes);
        pages[state.SeoId] = state;
        return Task.CompletedTask;
    }

    public Task DeletePageStateAsync(string seoId)
    {
        if (pages.TryRemove(seoId, out _)) Interlocked.Increment(ref Deletes);
        return Task.CompletedTask;
    }

    public Task UpsertMeasurementsAsync(IEnumerable<PartMeasurement> rows)
    {
        foreach (var row in rows)
        {
            Interlocked.Increment(ref Writes);
            measurements.GetOrAdd(row.SeoId, _ => new())[(row.AttributeKey, row.Source, row.ListingKey)] = row;
        }
        return Task.CompletedTask;
    }

    public Task DeleteMeasurementsAsync(string seoId, IEnumerable<string> attributeKeys)
    {
        if (measurements.TryGetValue(seoId, out var rows))
        {
            var keys = attributeKeys.ToHashSet(StringComparer.Ordinal);
            foreach (var key in rows.Keys.Where(k => keys.Contains(k.Key)).ToList())
                if (rows.TryRemove(key, out _)) Interlocked.Increment(ref Deletes);
        }
        return Task.CompletedTask;
    }

    public Task DeleteMeasurementsAsync(string seoId)
    {
        if (measurements.TryRemove(seoId, out var rows)) Interlocked.Add(ref Deletes, rows.Count);
        return Task.CompletedTask;
    }

    public Task DeleteMeasurementRowsAsync(string seoId, string attributeKey, string source, string listingKey)
    {
        if (measurements.TryGetValue(seoId, out var rows) && rows.TryRemove((attributeKey, source, listingKey), out _))
            Interlocked.Increment(ref Deletes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PartMeasurement>> GetMeasurementsAsync(string seoId) =>
        Task.FromResult<IReadOnlyList<PartMeasurement>>(measurements.TryGetValue(seoId, out var rows)
            ? rows.Values.OrderBy(r => r.AttributeKey, StringComparer.Ordinal).ThenBy(r => r.Source, StringComparer.Ordinal).ThenBy(r => r.ListingKey, StringComparer.Ordinal).ToList()
            : []);

    public Task UpsertSignatureAsync(PartSignatureRow row)
    {
        Interlocked.Increment(ref Writes);
        signatures.GetOrAdd((row.PartClass, row.PartKey), _ => new())[row.SeoId] = row;
        return Task.CompletedTask;
    }

    public Task DeleteSignatureAsync(string partClass, string partKey, string seoId)
    {
        if (signatures.TryGetValue((partClass, partKey), out var rows) && rows.TryRemove(seoId, out _))
            Interlocked.Increment(ref Deletes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PartSignatureRow>> GetBySignatureAsync(string partClass, string partKey, int limit) =>
        Task.FromResult<IReadOnlyList<PartSignatureRow>>(signatures.TryGetValue((partClass, partKey), out var rows)
            ? rows.Values.OrderBy(r => r.SeoId, StringComparer.Ordinal).Take(limit).ToList()
            : []);

    public Task UpsertPartNumberAsync(PartNumberRow row)
    {
        Interlocked.Increment(ref Writes);
        partNumbers.GetOrAdd(row.PartNumberKey, _ => new())[row.SeoId] = row;
        return Task.CompletedTask;
    }

    public Task DeletePartNumberAsync(string partNumberKey, string seoId)
    {
        if (partNumbers.TryGetValue(partNumberKey, out var rows) && rows.TryRemove(seoId, out _))
            Interlocked.Increment(ref Deletes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PartNumberRow>> GetByPartNumberAsync(string partNumberKey, int limit) =>
        Task.FromResult<IReadOnlyList<PartNumberRow>>(partNumbers.TryGetValue(partNumberKey, out var rows)
            ? rows.Values.OrderBy(r => r.SeoId, StringComparer.Ordinal).Take(limit).ToList()
            : []);

    public Task UpsertFitAsync(PartFitRow fit)
    {
        Interlocked.Increment(ref Writes);
        fitsByMachine.GetOrAdd(fit.MachineKey, _ => new())[fit.SeoId] = fit;
        fitsByPart.GetOrAdd(fit.SeoId, _ => new())[fit.MachineKey] = fit;
        return Task.CompletedTask;
    }

    public Task DeleteFitAsync(string machineKey, string seoId)
    {
        if (fitsByMachine.TryGetValue(machineKey, out var byMachine) && byMachine.TryRemove(seoId, out _))
            Interlocked.Increment(ref Deletes);
        if (fitsByPart.TryGetValue(seoId, out var byPart))
            byPart.TryRemove(machineKey, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PartFitRow>> GetPartsForMachineAsync(string machineKey, int limit) =>
        Task.FromResult<IReadOnlyList<PartFitRow>>(fitsByMachine.TryGetValue(machineKey, out var rows)
            ? rows.Values.OrderBy(r => r.SeoId, StringComparer.Ordinal).Take(limit).ToList()
            : []);

    public Task<IReadOnlyList<PartFitRow>> GetMachinesForPartAsync(string seoId) =>
        Task.FromResult<IReadOnlyList<PartFitRow>>(fitsByPart.TryGetValue(seoId, out var rows)
            ? rows.Values.OrderBy(r => r.MachineKey, StringComparer.Ordinal).ToList()
            : []);

    public Task UpsertSameEdgeAsync(PartSameEdge edge)
    {
        Interlocked.Increment(ref Writes);
        edges.GetOrAdd(edge.SeoId, _ => new())[edge.OtherSeoId] = edge;
        var reverse = edge.Reverse();
        edges.GetOrAdd(reverse.SeoId, _ => new())[reverse.OtherSeoId] = reverse;
        return Task.CompletedTask;
    }

    public Task DeleteSameEdgeAsync(string seoId, string otherSeoId)
    {
        if (edges.TryGetValue(seoId, out var mine) && mine.TryRemove(otherSeoId, out _))
            Interlocked.Increment(ref Deletes);
        if (edges.TryGetValue(otherSeoId, out var theirs))
            theirs.TryRemove(seoId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PartSameEdge>> GetSameEdgesAsync(string seoId, int limit = IPartStore.MaxEdges) =>
        Task.FromResult<IReadOnlyList<PartSameEdge>>(edges.TryGetValue(seoId, out var rows)
            ? rows.Values.OrderBy(r => r.OtherSeoId, StringComparer.Ordinal).Take(limit).ToList()
            : []);
}
