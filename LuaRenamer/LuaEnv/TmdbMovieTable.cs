// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbMovie)]
public partial class TmdbMovieTable : LuaTableWriter
{
    internal TmdbMovieTable(LuaTable t, LuaFunction getname) : base(t)
        => _t["getname"] = getname;

    [LuaField("TMDB movie ID")]
    public required long id { init => Set(value); }

    [LuaField("All available titles for the movie")]
    public required LuaArray<LuaRef<TitleTable>> titles { init => Set(value.Table); }

    [LuaField("Default movie title")]
    public required string? defaultname { init => Set(value); }

    [LuaField("Preferred movie title")]
    public required string? preferredname { init => Set(value); }

    [LuaField("Movie rating")]
    public required double rating { init => Set(value); }

    [LuaField("Whether the movie is age-restricted")]
    public required bool restricted { init => Set(value); }

    [LuaField("List of production studios")]
    public required LuaArray<string> studios { init => Set(value.Table); }

    [LuaField("Air date of the movie")]
    public required LuaRef<DateTimeTable>? airdate { init => Set(value?.Table); }

    [LuaType(LuaTypeNames.function, "Get the movie title in the specified language")]
    [LuaParameter(nameof(lang), nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang) => GetFunc([lang], ':');

    public static implicit operator LuaRef<TmdbMovieTable>(TmdbMovieTable t) => new(t._t);
}
