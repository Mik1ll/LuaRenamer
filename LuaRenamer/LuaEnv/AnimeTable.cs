// ReSharper disable InconsistentNaming

using System;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Anime)]
public partial class AnimeTable : LuaTableWriter
{
    internal AnimeTable(LuaTable t) : base(t, _classidVal) { }

    [LuaField("Get the anime title in the specified language")]
    public required LuaFunctionRef<Func<TitleLanguage, bool?, string?>> getname { init => Set(value.Value); }

    [LuaField("First air date of the anime")]
    public required LuaRef<DateTimeTable>? airdate { init => Set(value?.Table); }

    [LuaField("Last air date of the anime")]
    public required LuaRef<DateTimeTable>? enddate { init => Set(value?.Table); }

    [LuaField("Average rating of the anime")]
    public required double rating { init => Set(value); }

    [LuaField("Whether the anime is age-restricted")]
    public required bool restricted { init => Set(value); }

    [LuaField("Type of the anime (Movie, TVSeries, etc.)")]
    public required AnimeType type { init => Set(value.ToString()); }

    [LuaField("The preferred title for the anime")]
    public required string preferredname { init => Set(value); }

    [LuaField("The default title for the anime")]
    public required string defaultname { init => Set(value); }

    [LuaField("AniDB anime ID")]
    public required long id { init => Set(value); }

    [LuaField("All available titles for the anime")]
    public required LuaArray<LuaRef<TitleTable>> titles { init => Set(value.Table); }

    [LuaField("Count of episodes by type")]
    public required LuaMap<EpisodeType, long> episodecounts { init => Set(value.Table); }

    [LuaField("Related anime entries, not populated for nested Anime entries")]
    public required LuaArray<LuaRef<RelationTable>> relations { init => Set(value.Table); }

    [LuaField("List of studios that produced the anime")]
    public required LuaArray<string> studios { init => Set(value.Table); }

    [LuaField("List of anime series tags")]
    public required LuaArray<string> tags { init => Set(value.Table); }

    [LuaField("List of custom Shoko tags")]
    public required LuaArray<string> customtags { init => Set(value.Table); }

    [LuaField("List of seasons anime aired during")]
    public required LuaArray<LuaRef<SeasonTable>> seasons { init => Set(value.Table); }

    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";

    public static implicit operator LuaRef<AnimeTable>(AnimeTable t) => new(t._t);
}
