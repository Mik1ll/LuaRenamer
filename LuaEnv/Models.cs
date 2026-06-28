// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using LuaRenamer.LuaEnv.Attributes;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

// Vertical-slice port of AnimeTable + the schema it reaches: TitleTable, DateTimeTable,
// SeasonTable, RelationTable. Compare each property to its *Table counterpart: the type carries
// the same schema info, but there is no LuaRef/LuaArray/LuaMap/LuaEnumRef carrier and no
// `init => Set(...)` body — just plain CLR types. All marshaling happens in LuaSerializer.

public sealed record DateTimeModel : ILuaModel
{
    [LuaField("Year (four digits)")] public required long year { get; init; }
    [LuaField("Month (1-12)")] public required long month { get; init; }
    [LuaField("Day of the month (1-31)")] public required long day { get; init; }
    [LuaField("Day of the year (1-366)")] public required long yday { get; init; }
    [LuaField("Day of the week (1-7, 1 is Sunday)")] public required long wday { get; init; }
    [LuaField("Hour (0-23)")] public required long hour { get; init; }
    [LuaField("Minute (0-59)")] public required long min { get; init; }
    [LuaField("Second (0-59)")] public required long sec { get; init; }
    [LuaField("Is Daylight Saving Time in effect")] public required bool isdst { get; init; }
}

public sealed record TitleModel : ILuaModel
{
    [LuaField("The title text")] public required string name { get; init; }
    [LuaField("Language of the title")] public required TitleLanguage language { get; init; }  // was Set(value.ToString())
    [LuaField("ISO language code")] public required string languagecode { get; init; }
    [LuaField("Type of the title")] public required TitleType type { get; init; }              // was Set(value.ToString())
}

public sealed record SeasonModel : ILuaModel
{
    [LuaField("Season year")] public required long year { get; init; }
    [LuaField("Season aired")] public required YearlySeason season { get; init; }
}

public sealed record RelationModel : ILuaModel
{
    [LuaField("The related anime")] public required AnimeModel anime { get; init; }            // was LuaRef<AnimeTable>
    [LuaField("Type of relation between the anime")] public required RelationType type { get; init; }
}

public sealed record AnimeModel : ILuaModel
{
    // was LuaMethodRef<AnimeTitleDelegate>; the ':' method-call syntax is now a [LuaField] flag.
    [LuaField("Get the anime title in the specified language", Method = true)]
    public required LuaFn<AnimeTitleDelegate> getname { get; init; }

    [LuaField("First air date of the anime")] public DateTimeModel? airdate { get; init; }     // was LuaRef<DateTimeTable>?
    [LuaField("Last air date of the anime")] public DateTimeModel? enddate { get; init; }
    [LuaField("Average rating of the anime")] public required double rating { get; init; }
    [LuaField("Whether the anime is age-restricted")] public required bool restricted { get; init; }
    [LuaField("Type of the anime (Movie, TVSeries, etc.)")] public required AnimeType type { get; init; }
    [LuaField("The preferred title for the anime")] public required string preferredname { get; init; }
    [LuaField("The default title for the anime")] public required string defaultname { get; init; }
    [LuaField("AniDB anime ID")] public required long id { get; init; }

    [LuaField("All available titles for the anime")]
    public required IReadOnlyList<TitleModel> titles { get; init; }                            // was LuaArray<LuaRef<TitleTable>>

    [LuaField("Count of episodes by type")]
    public required IReadOnlyDictionary<EpisodeType, long> episodecounts { get; init; }        // was LuaMap<EpisodeType, long>

    [LuaField("Related anime entries, not populated for nested Anime entries")]
    public required IReadOnlyList<RelationModel> relations { get; init; }

    [LuaField("List of studios that produced the anime")]
    public required IReadOnlyList<string> studios { get; init; }                               // was LuaArray<string>

    [LuaField("List of anime series tags")]
    public required IReadOnlyList<string> tags { get; init; }

    [LuaField("List of custom Shoko tags")]
    public required IReadOnlyList<string> customtags { get; init; }

    [LuaField("List of seasons anime aired during")]
    public required IReadOnlyList<SeasonModel> seasons { get; init; }
}
