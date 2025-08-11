// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.TmdbShow)]
public class TmdbShowTable : Table
{
    [LuaType(LuaTypeNames.integer, "TMDB show ID")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]", "All available titles for the show")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string, "Default show title")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.@string, "Preferred show title")]
    public string preferredname => Get();

    [LuaType(LuaTypeNames.number, "Show rating")]
    public string rating => Get();

    [LuaType(LuaTypeNames.boolean, "Whether the show is age-restricted")]
    public string restricted => Get();

    [LuaType($"{LuaTypeNames.@string}[]", "List of production studios")]
    public string studios => Get();

    [LuaType(LuaTypeNames.integer, "Total number of episodes")]
    public string episodecount => Get();

    [LuaType($"{LuaTypeNames.DateTime}|{LuaTypeNames.nil}", "Air date of the show")]
    public string airdate => Get();

    [LuaType($"{LuaTypeNames.DateTime}|{LuaTypeNames.nil}", "End date of the show")]
    public string enddate => Get();

    [LuaType(LuaTypeNames.function, "Get the show title in the specified language")]
    [LuaParameter(nameof(lang), nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang) => GetFunc([lang], ':');
}
