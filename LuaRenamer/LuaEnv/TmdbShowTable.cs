// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbShow)]
public partial class TmdbShowTable : LuaTableWriter
{
    internal TmdbShowTable(LuaTable t, LuaFunction getname) : base(t)
        => _t["getname"] = getname;

    [LuaField("TMDB show ID")]
    public required long id { init => Set(value); }

    [LuaField("All available titles for the show")]
    public required LuaArray<LuaRef<TitleTable>> titles { init => Set(value.Table); }

    [LuaField("Default show title")]
    public required string? defaultname { init => Set(value); }

    [LuaField("Preferred show title")]
    public required string? preferredname { init => Set(value); }

    [LuaField("Show rating")]
    public required double rating { init => Set(value); }

    [LuaField("Whether the show is age-restricted")]
    public required bool restricted { init => Set(value); }

    [LuaField("List of production studios")]
    public required LuaArray<string> studios { init => Set(value.Table); }

    [LuaField("Total number of episodes")]
    public required long episodecount { init => Set(value); }

    [LuaField("Air date of the show")]
    public required LuaRef<DateTimeTable>? airdate { init => Set(value?.Table); }

    [LuaField("End date of the show")]
    public required LuaRef<DateTimeTable>? enddate { init => Set(value?.Table); }

    [LuaType(LuaTypeNames.function, "Get the show title in the specified language")]
    [LuaParameter(nameof(lang), nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang) => GetFunc([lang], ':');

    [LuaField("List of seasons show aired during")]
    public required LuaArray<LuaRef<SeasonTable>> seasons { init => Set(value.Table); }

    public static implicit operator LuaRef<TmdbShowTable>(TmdbShowTable t) => new(t._t);
}
