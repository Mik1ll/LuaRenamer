// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

public partial class TmdbEpisodeTable : LuaTableWriter
{
    internal TmdbEpisodeTable(LuaTable t) : base(t) { }

    [LuaField("Get the title in the specified language")]
    public required LuaMethodRef<TitleDelegate> getname { init => Set(value.Value); }

    [LuaField("TMDB episode ID")]
    public required long id { init => Set(value); }

    [LuaField("TMDB show ID")]
    public required long showid { init => Set(value); }

    [LuaField("All available titles for the episode")]
    public required LuaArray<LuaRef<TitleTable>> titles { init => Set(value.Table); }

    [LuaField("Default episode title")]
    public required string? defaultname { init => Set(value); }

    [LuaField("Preferred episode title")]
    public required string? preferredname { init => Set(value); }

    [LuaField("Type of episode")]
    public required EpisodeType type { init => Set(value.ToString()); }

    [LuaField("Episode number within the season")]
    public required long number { init => Set(value); }

    [LuaField("Season number")]
    public required long? seasonnumber { init => Set(value); }

    [LuaField("Air date of the episode")]
    public required LuaRef<DateTimeTable>? airdate { init => Set(value?.Table); }

    public static implicit operator LuaRef<TmdbEpisodeTable>(TmdbEpisodeTable t) => new(t._t);
}
