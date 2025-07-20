// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbEpisode)]
public class TmdbEpisodeTable : Table
{
    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("TMDB episode ID")]
    public string id => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("TMDB show ID")]
    public string showid => Get();

    [LuaType($"{LuaTypeNames.Title}[]")]
    [LuaDescription("All available titles for the episode")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Default episode title")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Preferred episode title")]
    public string preferredname => Get();

    [LuaType(nameof(EnumsTable.EpisodeType))]
    [LuaDescription("Type of episode")]
    public string type => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Episode number within the season")]
    public string number => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Season number")]
    public string seasonnumber => Get();

    [LuaType(LuaTypeNames.function)]
    [LuaDescription("Get the episode title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType(LuaTypeNames.@string, Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
