// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbShow)]
public class TmdbShowTable : Table
{
    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("TMDB show ID")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]")]
    [LuaDescription("All available titles for the show")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Default show title")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Preferred show title")]
    public string preferredname => Get();

    [LuaType(LuaTypeNames.number)]
    [LuaDescription("Show rating")]
    public string rating => Get();

    [LuaType(LuaTypeNames.boolean)]
    [LuaDescription("Whether the show is age-restricted")]
    public string restricted => Get();

    [LuaType($"{LuaTypeNames.@string}[]")]
    [LuaDescription("List of production studios")]
    public string studios => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Total number of episodes")]
    public string episodecount => Get();

    [LuaType(LuaTypeNames.function)]
    [LuaDescription("Get the show title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType(LuaTypeNames.@string, Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');
}
