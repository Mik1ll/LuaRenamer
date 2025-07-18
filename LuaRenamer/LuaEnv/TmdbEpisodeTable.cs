// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class TmdbEpisodeTable : Table
{
    [LuaType("integer")]
    [LuaDescription("TMDB episode ID")]
    public string id => Get();

    [LuaType("integer")]
    [LuaDescription("TMDB show ID")]
    public string showid => Get();

    [LuaType("Title[]")]
    [LuaDescription("All available titles for the episode")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType("string")]
    [LuaDescription("Default episode title")]
    public string defaultname => Get();

    [LuaType("string")]
    [LuaDescription("Preferred episode title")]
    public string preferredname => Get();

    [LuaType("EpisodeType")]
    [LuaDescription("Type of episode")]
    public string type => Get();

    [LuaType("integer")]
    [LuaDescription("Episode number within the season")]
    public string number => Get();

    [LuaType("integer")]
    [LuaDescription("Season number")]
    public string seasonnumber => Get();

    [LuaType("function")]
    [LuaDescription("Get the episode title in the specified language")]
    [LuaParameter("lang", "Language", "The language to get the title in")]
    [LuaReturnType("string", Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
