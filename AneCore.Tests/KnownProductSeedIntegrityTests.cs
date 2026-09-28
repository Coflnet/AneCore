using Coflnet.Ane;
using Coflnet.Ane.KnownProducts;

namespace AneCore.Tests;

/// <summary>
/// Integrity checks over the full embedded seed catalogue (apple-iphones.json plus every category added
/// by tools/catalog-import/build_seed.py) - task item C. AneNotifier's KnownProductCore.Tests.cs has its
/// own narrower "PossibleAttributeKeys" / alias-collision checks scoped to the original iPhone-only seed;
/// these run over the whole catalogue and additionally verify UnifiedCategories.json paths and the
/// alias min-length/non-numeric rule.
/// </summary>
[TestFixture]
public class KnownProductSeedIntegrityTests
{
    private static readonly HashSet<string> AllowedAttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "storage_size", "ram_size", "screen_size", "color", "size", "platform", "registration_year",
        "gender", "fashion_type", "material", "condition", "os", "os_version",
    };

    private IReadOnlyList<KnownProduct> seed = null!;
    private UnifiedCategoryService categoryService = null!;

    [SetUp]
    public void SetUp()
    {
        seed = KnownProductSeed.LoadAll(includeExpandedCatalog: true);
        categoryService = new UnifiedCategoryService();
    }

    [Test]
    public void Seed_LoadsMoreThanTheOriginalIphoneOnlyCatalogue()
    {
        // Regression guard: fails loudly if the wildcard EmbeddedResource glob in AneCore.csproj ever
        // stops picking up the additional Seed/*.json files (e.g. someone reverts to the old hardcoded
        // ResourceNames list in KnownProductSeed).
        Assert.That(seed.Count, Is.GreaterThan(23));
    }

    [Test]
    public void Ids_AreUniqueAcrossAllSeedFiles()
    {
        var duplicates = seed.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.That(duplicates, Is.Empty, $"Duplicate ids: {string.Join(", ", duplicates)}");
    }

    [Test]
    public void EveryEntry_HasSourceAndNonEmptyBrand()
    {
        foreach (var product in seed)
        {
            Assert.That(product.Source, Does.StartWith("seed:"), $"{product.Id} has no seed source");
            Assert.That(product.Brand, Is.Not.Empty, $"{product.Id} has no brand");
            Assert.That(product.Name, Is.Not.Empty, $"{product.Id} has no name");
        }
    }

    [Test]
    public void EveryEntry_HasNonEmptyModelAndCategory()
    {
        // Regression: sony-wh-1000xm4's seeded product row had a null Model in production even though
        // this catalogue entry's own "model" field was never empty - the seeder just never overwrote a
        // pre-existing row (see AneNotifier's ProductGrouper.CorrectKnownProductRowsAsync). Locking the
        // catalogue side down here so a future entry cannot reintroduce a missing model/category the same
        // way.
        foreach (var product in seed)
        {
            Assert.That(product.Model, Is.Not.Empty, $"{product.Id} has no model");
            Assert.That(product.Categories, Is.Not.Empty, $"{product.Id} has no category path");
        }
    }

    /// <summary>
    /// Every distinct category path used anywhere in the seed catalogue, reviewed by hand (see this
    /// task's final report) - a fixed allow-list rather than deriving "valid" from the data itself, so a
    /// future entry that lands in an unrelated category (the live "VR headset under Autoteile &amp;
    /// Reifen" / "phone under Videospiele" bugs this guards against, both caused by a pre-existing product
    /// row the seeder never corrected, not by the catalogue) fails loudly instead of silently joining
    /// "the current set". Extend this list only after checking the new path is a real, deliberate
    /// UnifiedCategories.json label path for the vertical (see <see cref="CategoryPaths_ExistInUnifiedCategories"/>,
    /// which checks the labels are real but not that they are one of these already-reviewed paths).
    /// </summary>
    private static readonly HashSet<string> AllowedCategoryPaths = new(StringComparer.Ordinal)
    {
        "Bekleidung & Accessoires>Schmuck>Armbanduhren & Taschenuhren",
        "Elektronik>Audio>Audiokomponenten>Kopfhörer & Headsets>Kopfhörer",
        "Elektronik>Computer>Datenbrillen",
        "Elektronik>Computer>Desktop-Computer",
        "Elektronik>Computer>Laptops",
        "Elektronik>Computer>Tablet-PCs",
        "Elektronik>Elektronische Leiterplatten & Bauteile>Leiterplatten>Computer-Leiterplatten>Motherboards",
        "Elektronik>Elektronisches Zubehör>Computerkomponenten>Computer-Steckkarten>Grafikkarten & Videoadapter",
        "Elektronik>Elektronisches Zubehör>Computerkomponenten>Prozessoren",
        "Elektronik>Elektronisches Zubehör>Computerkomponenten>Speichergeräte>Festplatten",
        "Elektronik>Elektronisches Zubehör>Speicher>RAM",
        "Elektronik>Kommunikationsgeräte>Telefone>Mobiltelefone",
        "Elektronik>Video>Computermonitore",
        "Elektronik>Videospielkonsolen",
        "Kameras & Optik>Kameras>Digitalkameras",
    };

    [Test]
    public void Categories_AreFromTheFixedAllowedList()
    {
        var unexpected = seed
            .Select(p => (p.Id, Path: string.Join(">", p.Categories)))
            .Where(p => !AllowedCategoryPaths.Contains(p.Path))
            .ToList();
        Assert.That(unexpected, Is.Empty,
            $"Products with a category path outside the reviewed allow-list: {string.Join(" | ", unexpected.Select(p => $"{p.Id}: '{p.Path}'"))}");
    }

    [Test]
    public void Name_NeverRepeatsBrandWordConsecutively()
    {
        // Regression: build_meta_quest() built every VR headset's name as "Meta " + model, but model
        // ("Meta Quest 2") already started with the brand word, producing "Meta Meta Quest 2".
        foreach (var product in seed)
        {
            if (string.IsNullOrWhiteSpace(product.Brand))
                continue;
            var tokens = product.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length - 1; i++)
            {
                var repeated = string.Equals(tokens[i], tokens[i + 1], StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[i], product.Brand, StringComparison.OrdinalIgnoreCase);
                Assert.That(repeated, Is.False,
                    $"{product.Id}: name '{product.Name}' repeats brand word '{product.Brand}'");
            }
        }
    }

    [Test]
    public void Model_DoesNotAlreadyStartWithBrand_WhenNameIsBrandPlusModelConcatenation()
    {
        // Whenever a builder assembles Name as the naive "{Brand} {Model}" concatenation, Model must not
        // itself already start with the brand word - that combination is exactly how the Meta Quest
        // regression above ("Meta " + "Meta Quest 2") happened. A builder whose Model already carries the
        // brand (e.g. OnePlus's numbered flagships) must instead set Name = Model directly (see
        // full_phone_name in tools/catalog-import/build_seed.py), which this check allows.
        foreach (var product in seed)
        {
            if (string.IsNullOrEmpty(product.Brand) || string.IsNullOrEmpty(product.Model))
                continue;
            var concatenated = $"{product.Brand} {product.Model}";
            if (!string.Equals(product.Name, concatenated, StringComparison.Ordinal))
                continue; // name was not built by naive concatenation - nothing to check here

            var brandPrefix = product.Brand + " ";
            Assert.That(product.Model.StartsWith(brandPrefix, StringComparison.OrdinalIgnoreCase), Is.False,
                $"{product.Id}: name is Brand+Model concatenation but model '{product.Model}' already starts with brand '{product.Brand}'");
        }
    }

    [Test]
    public void Aliases_AreAtLeastThreeCharsAndNotPurelyNumeric()
    {
        foreach (var product in seed)
        {
            foreach (var alias in product.Aliases)
            {
                var normalized = KnownProductMatcher.Normalize(alias);
                Assert.That(normalized.Replace(" ", "").Length, Is.GreaterThanOrEqualTo(3),
                    $"{product.Id} has an alias too short to be safe: '{alias}'");
                Assert.That(normalized.All(c => char.IsDigit(c) || c == ' '), Is.False,
                    $"{product.Id} has a purely numeric alias: '{alias}'");
            }
        }
    }

    [Test]
    public void PossibleAttributeKeys_AreAllRecognized()
    {
        foreach (var product in seed)
        {
            foreach (var key in product.PossibleAttributes.Keys)
            {
                Assert.That(AllowedAttributeKeys.Contains(key), Is.True,
                    $"{product.Id} declares unrecognized attribute key '{key}'");
            }
        }
    }

    [Test]
    public void CategoryPaths_ExistInUnifiedCategories()
    {
        // Build the set of every label appearing anywhere in UnifiedCategories.json (any depth), since
        // KnownProduct.Categories stores full label paths, not slugs, and a path is valid here as long
        // as each of its own labels is a real label in the tree (see task: "never invent labels").
        var allLabels = new HashSet<string>(StringComparer.Ordinal);
        void Walk(UnifiedCategory c)
        {
            allLabels.Add(c.Label);
            foreach (var sub in c.SubCategories ?? new List<UnifiedCategory>())
                Walk(sub);
        }
        foreach (var root in categoryService.GetTopLevelCategories())
            Walk(root);

        var missing = new List<string>();
        foreach (var product in seed)
        {
            foreach (var label in product.Categories)
            {
                if (!allLabels.Contains(label))
                    missing.Add($"{product.Id}: '{label}'");
            }
        }
        Assert.That(missing, Is.Empty, $"Category labels not found in UnifiedCategories.json: {string.Join(" | ", missing.Take(20))}");
    }

    [Test]
    public void Aliases_AreNotClaimedIdenticallyByTwoDifferentProducts()
    {
        // The only real "collision" the matcher (token-sequence equality, see
        // KnownProductMatcher.BuildIndex/Match) can be confused by is two products claiming the exact
        // same normalized alias. A shorter alias whose tokens are merely a strict PREFIX of a longer
        // alias belonging to a different product (e.g. "iphone 14" / "iphone 14 pro") is not a collision
        // at all - longest-alias-wins already resolves it deterministically, which is exactly the
        // task's "alias collisions only allowed where one alias strictly extends the other by tokens"
        // rule. (An earlier version of this test compared normalized alias STRINGS with StartsWith
        // instead of token arrays, which produced false positives like "apple watch se" vs "apple watch
        // series 1" - "se" is a character-prefix of "series" but a different 3rd TOKEN, so the matcher
        // never confuses them.)
        var allAliases = seed.SelectMany(p => p.Aliases.Select(a => (Product: p.Id, Alias: KnownProductMatcher.Normalize(a))))
            .ToList();

        foreach (var group in allAliases.GroupBy(a => a.Alias, StringComparer.Ordinal))
        {
            var distinctProducts = group.Select(g => g.Product).Distinct().ToList();
            Assert.That(distinctProducts, Has.Count.EqualTo(1),
                $"Alias '{group.Key}' is claimed identically by multiple products: {string.Join(", ", distinctProducts)}");
        }
    }

    [Test]
    public void EveryProduct_HasAtLeastOneAlias()
    {
        var missing = seed.Where(p => p.Aliases.Count == 0).Select(p => p.Id).ToList();
        Assert.That(missing, Is.Empty, $"Products with no aliases: {string.Join(", ", missing)}");
    }
}
