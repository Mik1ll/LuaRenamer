// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class TmdbMovieTable : Table
{
    [LuaType("integer")]
    [LuaDescription("TMDB movie ID")]
    public string id => Get();

    [LuaType("Title[]")]
    [LuaDescription("All available titles for the movie")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType("string")]
    [LuaDescription("Default movie title")]
    public string defaultname => Get();

    [LuaType("string")]
    [LuaDescription("Preferred movie title")]
    public string preferredname => Get();

    [LuaType("number")]
    [LuaDescription("Movie rating")]
    public string rating => Get();

    [LuaType("boolean")]
    [LuaDescription("Whether the movie is age-restricted")]
    public string restricted => Get();

    [LuaType("string[]")]
    [LuaDescription("List of production studios")]
    public string studios => Get();

    [LuaType("function")]
    [LuaDescription("Get the movie title in the specified language")]
    [LuaParameter("lang", "Language", "The language to get the title in")]
    [LuaReturnType("string", Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
