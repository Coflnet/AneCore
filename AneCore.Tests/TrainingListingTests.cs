using Coflnet.Ane;
using Coflnet.Ane.TrainingListings;
using MessagePack;

namespace AneCore.Tests;

[TestFixture]
public class TrainingTextSanitizerTests
{
    // German, Dutch, French and international formats
    [TestCase("Rufen Sie an: 0151 12345678 danach", "Rufen Sie an: [phone] danach")]
    [TestCase("Tel. 0176/1234567.", "Tel. [phone].")]
    [TestCase("Handy: +49 151 12345678", "Handy: [phone]")]
    [TestCase("Handy: +49 (0) 151 1234567 bitte", "Handy: [phone] bitte")]
    [TestCase("0049 151 1234567", "[phone]")]
    [TestCase("Festnetz (0511) 12 34 56 78", "Festnetz [phone]")]
    [TestCase("030-1234567-0", "[phone]")]
    [TestCase("030 / 123 456 78", "[phone]")]
    [TestCase("WhatsApp 01512345678!", "WhatsApp [phone]!")]
    [TestCase("bel 06-12345678 of mail", "bel [phone] of mail")]
    [TestCase("bel 06 12 34 56 78", "bel [phone]")]
    [TestCase("06-1234 5678", "[phone]")]
    [TestCase("+31 6 12345678", "[phone]")]
    [TestCase("+31 (0)6-12345678", "[phone]")]
    [TestCase("0031 6 12345678", "[phone]")]
    [TestCase("020-1234567", "[phone]")]
    [TestCase("appelez le 06 12 34 56 78 svp", "appelez le [phone] svp")]
    [TestCase("01.23.45.67.89", "[phone]")]
    [TestCase("+33 6 12 34 56 78", "[phone]")]
    [TestCase("+33 (0)6 12 34 56 78", "[phone]")]
    [TestCase("+33612345678", "[phone]")]
    [TestCase("+41 79 123 45 67", "[phone]")]
    public void Phone_IsMasked(string input, string expected) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(expected));

    [TestCase("KDL-32WD755 Fernseher")]
    [TestCase("Sony KDL-32WD755")]
    [TestCase("Preis 1.234,56 EUR")]
    [TestCase("nur 0,99 Euro")]
    [TestCase("Art.-Nr. 0815-4711-9999")]
    [TestCase("Artikelnummer: 0815 4711 12")]
    [TestCase("EAN 4006381333931")]
    [TestCase("Seriennummer 0123456789012")]
    [TestCase("Jeans Gr. W32/L34 blau")]
    [TestCase("Größe 38, 40 oder 42")]
    [TestCase("gekauft am 12.05.2026")]
    [TestCase("gekauft am 03.10.2026 neu")]
    [TestCase("Mo-Fr 09.00-12.00 14.00-18.00 Uhr")]
    [TestCase("iPhone 15 Pro 256GB 5G")]
    [TestCase("Baujahr 2019, 85.000 km, 110 PS")]
    [TestCase("RTX 4070 12GB 192-bit")]
    [TestCase("Bestellnummer 4711-0815-1234")]
    [TestCase("Ma 10 mm x 0,5 mm")]
    public void ModelNumbersPricesAndArticleNumbers_AreNotMasked(string input) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(input));

    [TestCase("Schreib mir: max.mustermann@web.de danke", "Schreib mir: [email] danke")]
    [TestCase("mail: jan_de-vries+ads@gmail.com", "mail: [email]")]
    [TestCase("contact: marie.dupont@orange.fr.", "contact: [email].")]
    [TestCase("max.mustermann (at) web.de", "[email]")]
    public void Email_IsMasked(string input, string expected) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(expected));

    [TestCase("IBAN DE89 3704 0044 0532 0130 00 bitte", "IBAN [iban] bitte")]
    [TestCase("IBAN DE89370400440532013000", "IBAN [iban]")]
    [TestCase("IBAN: NL91 ABNA 0417 1643 00.", "IBAN: [iban].")]
    [TestCase("FR14 2004 1010 0505 0001 3M02 606 Bank", "[iban] Bank")]
    [TestCase("DE89 3704 0044 0532 0130 00 Sparkasse", "[iban] Sparkasse")]
    public void Iban_IsMasked(string input, string expected) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(expected));

    [TestCase("SM-G991B 128GB")]
    [TestCase("AB12 Modell CD34")]
    [TestCase("Bosch GSR 12V-15 FC")]
    public void Iban_LookAlikesAreNotMasked(string input) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(input));

    [TestCase("Schreib per https://wa.me/4915112345678 oder", "Schreib per [link] oder")]
    [TestCase("t.me/mustermann", "[link]")]
    [TestCase("siehe www.instagram.com/shop_xy/ ok", "siehe [link] ok")]
    [TestCase("https://chat.whatsapp.com/AbCdEf123", "[link]")]
    [TestCase("m.me/mustermann", "[link]")]
    public void MessengerLinks_AreMasked(string input, string expected) =>
        Assert.That(TrainingTextSanitizer.Mask(input), Is.EqualTo(expected));

    [Test]
    public void OtherLinks_AreKept() =>
        Assert.That(TrainingTextSanitizer.Mask("Anleitung: https://www.sony.de/support/kdl"), Is.EqualTo("Anleitung: https://www.sony.de/support/kdl"));

    [Test]
    public void NullAndEmpty_StayAsTheyAre()
    {
        Assert.That(TrainingTextSanitizer.Mask(null), Is.Null);
        Assert.That(TrainingTextSanitizer.Mask(""), Is.EqualTo(""));
    }
}

[TestFixture]
public class TrainingListingBuilderTests
{
    private static Listing Sample(Platform platform = Platform.Kleinanzeigen) => new()
    {
        Id = "3180765538",
        Platform = platform,
        Title = "Sony KDL-32WD755 Fernseher",
        Description = "Top Zustand, Abholung oder Versand. Tel 0151 12345678, mail@example.com",
        DescriptionShort = "Top Zustand",
        Category = "Fernseher",
        Categories = ["Elektronik", "Fernseher"],
        Street = "Musterstr. 1",
        Locality = "Berlin",
        Region = "Berlin",
        Country = "DE",
        Contact = "Max Mustermann",
        UserId = "999",
        Latitude = 52.5f,
        Longitude = 13.4f,
        Price = 120,
        Currency = "EUR",
        PriceKind = PriceKind.Negotiatable,
        Shipping = "+ Versand ab 4,89 €",
        CreatedAt = new DateTime(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc),
        FoundAt = new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc),
        ImageUrls = ["https://img.kleinanzeigen.de/a.jpg"],
        Commercial = false,
        Attributes = new Dictionary<string, string>
        {
            ["Marke"] = "Sony", ["Zustand"] = "Gut", ["condition"] = "used", ["Anbieter Kontakt"] = "x", ["Seller"] = "y", ["Org Name"] = "z",
            ["phone"] = "0151 1", ["E-Mail"] = "a@b.de", ["Address"] = "Str", ["Hinweis"] = "ruf 0170 1234567 an"
        }
    };

    [TestCase(TrainingListingScope.Kept, null)]
    [TestCase(TrainingListingScope.Dropped, "furniture")]
    [TestCase(TrainingListingScope.Sampled, "cards")]
    public void Build_KeepsScopeDecision(string scope, string? reason)
    {
        var built = TrainingListingBuilder.Build(Sample(), scope, reason)!;

        Assert.That(built.Scope, Is.EqualTo(scope));
        Assert.That(built.ScopeReason, Is.EqualTo(reason));
        Assert.That(built.ListingId, Is.EqualTo("3180765538"));
        Assert.That(built.Platform, Is.EqualTo(Platform.Kleinanzeigen));
        Assert.That(built.Url, Is.EqualTo("https://www.kleinanzeigen.de/s-anzeige/copy/3180765538-1-1"));
        Assert.That(built.FirstSeenAt, Is.EqualTo(new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc)));
        Assert.That(built.Condition, Is.EqualTo("used"));
        Assert.That(built.Price, Is.EqualTo(120));
        Assert.That(built.Categories, Is.EqualTo(new[] { "Elektronik", "Fernseher" }));
    }

    [Test]
    public void Build_RemovesPersonalDataFieldsAndMasksText()
    {
        var built = TrainingListingBuilder.Build(Sample(), TrainingListingScope.Kept)!;

        Assert.That(built.Description, Is.EqualTo("Top Zustand, Abholung oder Versand. Tel [phone], [email]"));
        Assert.That(built.Title, Is.EqualTo("Sony KDL-32WD755 Fernseher"));
        Assert.That(built.Attributes!.Keys, Is.EquivalentTo(new[] { "Marke", "Zustand", "condition", "Hinweis" }));
        Assert.That(built.Attributes["Hinweis"], Is.EqualTo("ruf [phone] an"));
        // the message has no field for the seller: nothing of it can be in the serialized form
        var text = System.Text.Json.JsonSerializer.Serialize(built);
        foreach (var secret in new[] { "Mustermann", "999", "Musterstr", "52.5", "13.4", "0151", "mail@example.com" })
            Assert.That(text, Does.Not.Contain(secret));
        var names = typeof(TrainingListing).GetProperties().Select(p => p.Name).ToList();
        Assert.That(names, Has.None.Matches<string>(n => n.Contains("User") || n.Contains("Contact") || n.Contains("Street")
            || n.Contains("Latitude") || n.Contains("Longitude") || n.Contains("Seller")));
    }

    [TestCase("address", true)]
    [TestCase("sellerAddress", true)]
    [TestCase("contact/name", true)]
    [TestCase("E-Mail", true)]
    [TestCase("Telefon", true)]
    [TestCase("Anbieter Kontakt", true)]
    [TestCase("orgname", true)]
    [TestCase("Marke", false)]
    [TestCase("Modell", false)]
    [TestCase("Zustand", false)]
    [TestCase("Art", false)]
    [TestCase("Größe", false)]
    public void IsSellerDataAttributeKey_MatchesTheNotifierPatterns(string key, bool expected) =>
        Assert.That(TrainingListingBuilder.IsSellerDataAttributeKey(key), Is.EqualTo(expected));

    [Test]
    public void Build_UsesGivenImageUrlsAndDropsDuplicates()
    {
        var built = TrainingListingBuilder.Build(Sample(), TrainingListingScope.Kept, null,
            ["https://img.kleinanzeigen.de/a.jpg", "https://img.kleinanzeigen.de/b.jpg", "https://img.kleinanzeigen.de/a.jpg", "", "javascript:x"])!;

        Assert.That(built.ImageUrls, Is.EqualTo(new[] { "https://img.kleinanzeigen.de/a.jpg", "https://img.kleinanzeigen.de/b.jpg" }));
    }

    [TestCase(Platform.Vinted)]
    [TestCase(Platform.Ebay)]
    [TestCase(Platform.Unknown)]
    public void Build_RefusesForbiddenPlatforms(Platform platform) =>
        Assert.That(TrainingListingBuilder.Build(Sample(platform), TrainingListingScope.Kept), Is.Null);

    [Test]
    public void Build_RefusesListingsWithoutText()
    {
        var empty = Sample();
        empty.Title = null;
        empty.Description = null;
        empty.DescriptionShort = " ";
        Assert.That(TrainingListingBuilder.Build(empty, TrainingListingScope.Kept), Is.Null);
    }

    [Test]
    public void Build_NoPriceSentinelIsNull()
    {
        var listing = Sample();
        listing.Price = -1;
        Assert.That(TrainingListingBuilder.Build(listing, TrainingListingScope.Kept)!.Price, Is.Null);
    }

    [Test]
    public void MessagePack_RoundTrips()
    {
        var built = TrainingListingBuilder.Build(Sample(), TrainingListingScope.Sampled, "cards")!;
        var back = MessagePackSerializer.Deserialize<TrainingListing>(MessagePackSerializer.Serialize(built));

        Assert.That(back.ListingId, Is.EqualTo(built.ListingId));
        Assert.That(back.Scope, Is.EqualTo("sampled"));
        Assert.That(back.Attributes, Is.EquivalentTo(built.Attributes!));
        Assert.That(back.PriceKind, Is.EqualTo(PriceKind.Negotiatable));
    }

    [Test]
    public void Policy_RefusesVintedAndEbayWhateverIsConfigured()
    {
        var parsed = TrainingListingPolicy.ParsePlatforms("Kleinanzeigen, vinted,Ebay,Marktplaats,Nope");

        Assert.That(parsed.Allowed, Is.EquivalentTo(new[] { Platform.Kleinanzeigen, Platform.Marktplaats }));
        Assert.That(parsed.Refused, Is.EquivalentTo(new[] { Platform.Vinted, Platform.Ebay }));
        Assert.That(parsed.Unknown, Is.EqualTo(new[] { "Nope" }));
        Assert.That(parsed.Contains(Platform.Vinted), Is.False);
    }

    [Test]
    public void Policy_DefaultIsKleinanzeigenAndMarktplaats()
    {
        Assert.That(TrainingListingPolicy.ParsePlatforms(null).Allowed, Is.EquivalentTo(new[] { Platform.Kleinanzeigen, Platform.Marktplaats }));
        Assert.That(TrainingListingPolicy.ParsePlatforms("").Allowed, Is.Empty);
    }
}

[TestFixture]
public class TrainingListingStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static TrainingListing Row(string id, DateTime? firstSeen = null, Platform platform = Platform.Kleinanzeigen) => new()
    {
        Platform = platform, ListingId = id, Title = "t" + id, FirstSeenAt = firstSeen ?? Now, Scope = TrainingListingScope.Kept
    };

    [Test]
    public void Mapping_ShardOfIsStableAndInRange()
    {
        var shards = Enumerable.Range(0, 500).Select(i => TrainingListingKeys.ShardOf(2, i.ToString())).ToList();
        Assert.That(shards, Has.All.InRange(0, 15));
        Assert.That(shards.Distinct().Count(), Is.EqualTo(16));
        Assert.That(TrainingListingKeys.ShardOf(2, "77"), Is.EqualTo(TrainingListingKeys.ShardOf(2, "77".ToString())));
    }

    [Test]
    public void Statements_BindNoEnumAndDoNotAutoPageRows()
    {
        var listing = Row("1");
        listing.PriceKind = PriceKind.Negotiatable;
        var insert = CassandraTrainingListingStore.BuildInsertStatement(listing, "2026-09-30", 3, TimeSpan.FromDays(14));

        Assert.That(insert.QueryValues.Where(v => v != null).Select(v => v.GetType().IsEnum), Has.All.False);
        Assert.That(insert.QueryString, Does.Contain("IF NOT EXISTS USING TTL 1209600"));
        Assert.That(CassandraTrainingListingStore.BuildRowsStatement("d", 1, null, null, null).AutoPage, Is.False);
        Assert.That(CassandraTrainingListingStore.BuildRowsStatement("d", 1, 2, "x", [1, 2]).QueryString, Does.Contain("(platform, listing_id) > (?, ?)"));
        Assert.That(CassandraTrainingListingStore.CreateListingsCql, Does.Contain("PRIMARY KEY ((day, shard), platform, listing_id)"));
    }

    [Test]
    public async Task Add_IsIdempotent_AndCounts()
    {
        var store = new InMemoryTrainingListingStore(() => Now);

        Assert.That(await store.AddAsync(Row("1")), Is.True);
        Assert.That(await store.AddAsync(Row("1")), Is.False);
        Assert.That(await store.AddAsync(Row("2")), Is.True);

        var days = await store.ListDaysAsync();
        Assert.That(days, Has.Count.EqualTo(1));
        Assert.That(days[0].Day, Is.EqualTo("2026-09-30"));
        Assert.That(days[0].Rows, Is.EqualTo(2));
    }

    [Test]
    public async Task Add_UsesTheDayOfFirstSeen()
    {
        var store = new InMemoryTrainingListingStore(() => Now);
        await store.AddAsync(Row("1", Now.AddDays(-3)));
        await store.AddAsync(Row("2", Now));

        Assert.That((await store.ListDaysAsync()).Select(d => d.Day), Is.EqualTo(new[] { "2026-09-27", "2026-09-30" }));
    }

    [Test]
    public async Task Delete_RemovesFromEveryDayAndCount()
    {
        var store = new InMemoryTrainingListingStore(() => Now);
        await store.AddAsync(Row("1", Now.AddDays(-1)));
        await store.AddAsync(Row("1", Now));
        await store.AddAsync(Row("2", Now));

        Assert.That(await store.DeleteAsync(Platform.Kleinanzeigen, "1"), Is.EqualTo(2));
        Assert.That(await store.DeleteAsync(Platform.Kleinanzeigen, "1"), Is.EqualTo(0));
        Assert.That((await store.ListDaysAsync()).Sum(d => d.Rows), Is.EqualTo(1));
    }

    [Test]
    public async Task Rows_ExpireAfterTheTtl()
    {
        var clock = Now;
        var store = new InMemoryTrainingListingStore(() => clock, TimeSpan.FromDays(14));
        await store.AddAsync(Row("1"));
        clock = Now.AddDays(15);
        var shard = TrainingListingKeys.ShardOf((int)Platform.Kleinanzeigen, "1");

        var page = await store.ReadPartitionAsync("2026-09-30", shard, 10, null);
        Assert.That(await page.Rows.ToListAsync(), Is.Empty);
    }

    [Test]
    public async Task ReadPartition_PagesWithCursorUntilFinished()
    {
        var store = new InMemoryTrainingListingStore(() => Now);
        var ids = Enumerable.Range(0, 400).Select(i => (1000 + i).ToString()).ToList();
        foreach (var id in ids)
            await store.AddAsync(Row(id));
        var shard = TrainingListingKeys.ShardOf((int)Platform.Kleinanzeigen, ids[0]);
        var inShard = ids.Where(i => TrainingListingKeys.ShardOf((int)Platform.Kleinanzeigen, i) == shard).ToList();
        Assert.That(inShard.Count, Is.GreaterThan(5));

        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await store.ReadPartitionAsync("2026-09-30", shard, 5, cursor);
            var rows = await page.Rows.ToListAsync();
            Assert.That(rows.Count, Is.LessThanOrEqualTo(5));
            seen.AddRange(rows.Select(r => r.ListingId));
            cursor = page.NextCursor;
            pages++;
        } while (cursor != null);

        Assert.That(seen, Is.EqualTo(inShard.OrderBy(i => i, StringComparer.Ordinal).ToList()));
        Assert.That(pages, Is.EqualTo((inShard.Count + 4) / 5));
    }

    [Test]
    public async Task ReadPartition_ExactMultipleOfLimitHasNoDanglingCursor()
    {
        var store = new InMemoryTrainingListingStore(() => Now);
        var ids = Enumerable.Range(0, 200).Select(i => i.ToString()).Where(i => TrainingListingKeys.ShardOf(2, i) == 0).Take(4).ToList();
        foreach (var id in ids)
            await store.AddAsync(Row(id));

        var page = await store.ReadPartitionAsync("2026-09-30", 0, 4, null);

        Assert.That(await page.Rows.ToListAsync(), Has.Count.EqualTo(4));
        Assert.That(page.NextCursor, Is.Null);
    }

    [Test]
    public void Cursor_RoundTripsAndRejectsGarbage()
    {
        var cursor = TrainingListingCursor.Encode(2, "id:with:colons/ä");
        Assert.That(TrainingListingCursor.TryDecode(cursor, out var platform, out var id), Is.True);
        Assert.That((platform, id), Is.EqualTo((2, "id:with:colons/ä")));
        Assert.That(TrainingListingCursor.TryDecode("!!!", out _, out _), Is.False);
        Assert.That(TrainingListingCursor.TryDecode(null, out _, out _), Is.False);
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
