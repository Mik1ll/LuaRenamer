// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class TmdbShowTable : Table
{
    [LuaType("integer")]
    [LuaDescription("TMDB show ID")]
    public string id => Get();

    [LuaType("Title[]")]
    [LuaDescription("All available titles for the show")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType("string")]
    [LuaDescription("Default show title")]
    public string defaultname => Get();

    [LuaType("string")]
    [LuaDescription("Preferred show title")]
    public string preferredname => Get();

    [LuaType("number")]
    [LuaDescription("Show rating")]
    public string rating => Get();

    [LuaType("boolean")]
    [LuaDescription("Whether the show is age-restricted")]
    public string restricted => Get();

    [LuaType("string[]")]
    [LuaDescription("List of production studios")]
    public string studios => Get();

    [LuaType("integer")]
    [LuaDescription("Total number of episodes")]
    public string episodecount => Get();

    [LuaType("function")]
    [LuaDescription("Get the show title in the specified language")]
    [LuaParameter("lang", "Language", "The language to get the title in")]
    [LuaReturnType("string", Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
