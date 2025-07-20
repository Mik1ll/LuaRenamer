// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbMovie)]
public class TmdbMovieTable : Table
{
    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("TMDB movie ID")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]")]
    [LuaDescription("All available titles for the movie")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Default movie title")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Preferred movie title")]
    public string preferredname => Get();

    [LuaType(LuaTypeNames.number)]
    [LuaDescription("Movie rating")]
    public string rating => Get();

    [LuaType(LuaTypeNames.boolean)]
    [LuaDescription("Whether the movie is age-restricted")]
    public string restricted => Get();

    [LuaType($"{LuaTypeNames.@string}[]")]
    [LuaDescription("List of production studios")]
    public string studios => Get();

    [LuaType(LuaTypeNames.function)]
    [LuaDescription("Get the movie title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType(LuaTypeNames.@string, Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
