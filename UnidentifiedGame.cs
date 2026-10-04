namespace Coflnet.Ane;

/// <summary>
/// A game listing the grouping could not give a page of its own because its title names a series and no game ("Buzz Sony ps2", "Ben 10 ps2"), kept so it can be reviewed later and identified by its photo
/// (table <c>unidentified_games</c>). Partition key: (<see cref="Platform"/>, <see cref="Series"/>); clustering key: (<see cref="ListingPlatform"/>, <see cref="ListingId"/>), so a series of a platform is one partition that pages by listing.
/// <see cref="Platform"/> is the game platform of the listing ("PlayStation 2", the extractor's platform name), <see cref="ListingPlatform"/> the marketplace (<see cref="Coflnet.Ane.Platform"/> as an int: the Cassandra driver cannot map enums).
/// A listing is in the table until it is identified (it joined a page) or its row expires (<see cref="TtlSeconds"/>, refreshed whenever the listing is seen again). A second table, <c>unidentified_game_index</c>, finds the row of a listing without knowing its series.
/// </summary>
public class UnidentifiedGame
{
    /// <summary>Rows expire after 90 days without being seen again.</summary>
    public const int TtlSeconds = 90 * 24 * 3600;

    /// <summary>Everything the grouping tried so far had the title and the description (<see cref="StageDescription"/>); a later photo step sets <see cref="StagePhoto"/>.</summary>
    public const string StageTitle = "title", StageDescription = "description", StagePhoto = "photo";

    /// <summary>The game platform of the listing ("PlayStation 2", "Nintendo DS").</summary>
    public string Platform { get; set; } = "";
    /// <summary>The series label the title names ("Buzz!", "Ben 10", "Legacy of Kain").</summary>
    public string Series { get; set; } = "";
    /// <summary>The marketplace of the listing as a <see cref="Coflnet.Ane.Platform"/> int.</summary>
    public int ListingPlatform { get; set; }
    public string ListingId { get; set; } = "";
    public string? Title { get; set; }
    public double Price { get; set; }
    public string? Currency { get; set; }
    /// <summary>The first image of the listing.</summary>
    public string? ImageUrl { get; set; }
    /// <summary>When the listing was first seen by the marketplace crawler (<see cref="Listing.FoundAt"/>).</summary>
    public DateTime FoundAt { get; set; }
    /// <summary>What has been tried to identify the game: <see cref="StageTitle"/> (the title only), <see cref="StageDescription"/> (title and description) or <see cref="StagePhoto"/>.</summary>
    public string IdentifyStage { get; set; } = StageDescription;
    /// <summary>The detach evidence line (<c>game series "Buzz!" (13 titles on PlayStation 2), title not identified; description names no single game</c>).</summary>
    public string? Evidence { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>The key of <see cref="UnidentifiedGameIndexRow"/>.</summary>
    public static string ListingKey(int listingPlatform, string listingId) => $"{listingPlatform}:{listingId}";
}

/// <summary>The row of table <c>unidentified_game_index</c>: where the row of a listing is (its partition of <c>unidentified_games</c>), so a listing that gets identified can leave the list without knowing its series.</summary>
public class UnidentifiedGameIndexRow
{
    public string ListingKey { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Series { get; set; } = "";
}

/// <summary>A series of a platform that has unidentified listings and how many (<see cref="ProductTableService.ListUnidentifiedGameSeriesAsync"/>).</summary>
public record UnidentifiedGameSeries(string Platform, string Series, long Count);
