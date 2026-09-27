using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Ane.KnownProducts;

/// <summary>
/// Cassandra-backed <see cref="IKnownProductStore"/>. The table is created with an explicit CQL
/// statement (rather than <c>Table&lt;T&gt;.CreateIfNotExistsAsync()</c>) so the nested
/// <c>map&lt;text, frozen&lt;set&lt;text&gt;&gt;&gt;</c> column type for <see cref="KnownProduct.PossibleAttributes"/>
/// is exact - the driver's POCO-&gt;DDL inference does not reliably generate nested frozen collection
/// types. The <see cref="Table{TPoco}"/> mapping below is only used for statements (insert/select/delete),
/// which serialize against the live cluster schema rather than re-deriving it from the POCO.
/// </summary>
public class CassandraKnownProductStore : IKnownProductStore
{
    private readonly ISession session;
    private readonly Table<KnownProduct> table;
    private static bool tableInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    public CassandraKnownProductStore(ISession session)
    {
        this.session = session;

        var mapping = new MappingConfiguration()
            .Define(new Map<KnownProduct>()
                .TableName("known_products")
                .PartitionKey(p => p.Id)
                .Column(p => p.Id, cm => cm.WithName("id"))
                .Column(p => p.Brand, cm => cm.WithName("brand"))
                .Column(p => p.Model, cm => cm.WithName("model"))
                .Column(p => p.Name, cm => cm.WithName("name"))
                .Column(p => p.Categories, cm => cm.WithName("categories").WithDbType<List<string>>())
                .Column(p => p.Vertical, cm => cm.WithName("vertical"))
                .Column(p => p.Aliases, cm => cm.WithName("aliases").WithDbType<HashSet<string>>())
                .Column(p => p.PossibleAttributes, cm => cm.WithName("possible_attributes"))
                .Column(p => p.ExcludeTerms, cm => cm.WithName("exclude_terms").WithDbType<HashSet<string>>())
                .Column(p => p.Source, cm => cm.WithName("source"))
                .Column(p => p.VerifiedAt, cm => cm.WithName("verified_at")));

        table = new Table<KnownProduct>(session, mapping);
    }

    public async Task InitializeAsync()
    {
        if (tableInitialized) return;
        await InitLock.WaitAsync();
        try
        {
            if (tableInitialized) return;
            await session.ExecuteAsync(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS known_products (
                    id text PRIMARY KEY,
                    brand text,
                    model text,
                    name text,
                    categories list<text>,
                    vertical text,
                    aliases set<text>,
                    possible_attributes map<text, frozen<set<text>>>,
                    exclude_terms set<text>,
                    source text,
                    verified_at timestamp
                )"));
            tableInitialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public async Task<IReadOnlyList<KnownProduct>> GetAllAsync()
    {
        var rows = await table.ExecuteAsync();
        return rows.ToList();
    }

    public async Task<KnownProduct?> GetAsync(string id) =>
        await table.Where(p => p.Id == id).FirstOrDefault().ExecuteAsync();

    public async Task UpsertAsync(KnownProduct product) =>
        await table.Insert(product).ExecuteAsync();

    public async Task DeleteAsync(string id) =>
        await table.Where(p => p.Id == id).Delete().ExecuteAsync();
}
