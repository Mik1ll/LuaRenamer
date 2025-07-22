// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Anime)]
public class AnimeTable : Table
{
    [LuaType($"{LuaTypeNames.DateTime}|{LuaTypeNames.nil}", "First air date of the anime")]
    public DateTimeTable airdate => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.DateTime}|{LuaTypeNames.nil}", "Last air date of the anime")]
    public DateTimeTable enddate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.number, "Average rating of the anime")]
    public string rating => Get();

    [LuaType(LuaTypeNames.boolean, "Whether the anime is age-restricted")]
    public string restricted => Get();

    [LuaType(nameof(EnumsTable.AnimeType), "Type of the anime (Movie, TVSeries, etc.)")]
    public string type => Get();

    [LuaType(LuaTypeNames.@string, "The preferred title for the anime")]
    public string preferredname => Get();

    [LuaType(LuaTypeNames.@string, "The default title for the anime")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.integer, "Unique identifier for the anime")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]", "All available titles for the anime")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.function, "Get the anime title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaParameter("include_unofficial", $"{LuaTypeNames.boolean}|{LuaTypeNames.nil}", "Whether to include unofficial titles")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang, string? include_unofficial = null) => GetFunc([lang, include_unofficial], ':');

    [LuaType($"{LuaTypeNames.table}<{nameof(EnumsTable.EpisodeType)}, {LuaTypeNames.integer}>", "Count of episodes by type")]
    public string episodecounts => Get();

    [LuaType($"{LuaTypeNames.Relation}[]", "Related anime entries, not populated for nested Anime entries")]
    public ArrayTable<RelationTable> relations => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.@string}[]", "List of studios that produced the anime")]
    public string studios => Get();

    public string _classid => Get();
    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";
}
