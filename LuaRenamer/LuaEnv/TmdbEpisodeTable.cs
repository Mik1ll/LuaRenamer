// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbEpisode)]
public class TmdbEpisodeTable : Table
{
    [LuaType(LuaTypeNames.integer, "TMDB episode ID")]
    public string id => Get();

    [LuaType(LuaTypeNames.integer, "TMDB show ID")]
    public string showid => Get();

    [LuaType($"{LuaTypeNames.Title}[]", "All available titles for the episode")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string, "Default episode title")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.@string, "Preferred episode title")]
    public string preferredname => Get();

    [LuaType(nameof(EnumsTable.EpisodeType), "Type of episode")]
    public string type => Get();

    [LuaType(LuaTypeNames.integer, "Episode number within the season")]
    public string number => Get();

    [LuaType(LuaTypeNames.integer, "Season number")]
    public string seasonnumber => Get();

    [LuaType(LuaTypeNames.function, "Get the episode title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang) => GetFunc([lang], ':');
}
