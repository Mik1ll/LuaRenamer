// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Anime)]
public class AnimeTable : Table
{
    [LuaType(LuaTypeNames.DateTime, Nillable = true)]
    [LuaDescription("First air date of the anime")]
    public DateTimeTable airdate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.DateTime, Nillable = true)]
    [LuaDescription("Last air date of the anime")]
    public DateTimeTable enddate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.number)]
    [LuaDescription("Average rating of the anime")]
    public string rating => Get();

    [LuaType(LuaTypeNames.boolean)]
    [LuaDescription("Whether the anime is age-restricted")]
    public string restricted => Get();

    [LuaType(nameof(EnumsTable.AnimeType))]
    [LuaDescription("Type of the anime (Movie, TVSeries, etc.)")]
    public string type => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("The preferred title for the anime")]
    public string preferredname => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("The default title for the anime")]
    public string defaultname => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Unique identifier for the anime")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]")]
    [LuaDescription("All available titles for the anime")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.function)]
    [LuaDescription("Get the anime title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaParameter("include_unofficial", $"{LuaTypeNames.boolean}|{LuaTypeNames.nil}", "Whether to include unofficial titles")]
    [LuaReturnType(LuaTypeNames.@string, Nillable = true)]
    public string getname(string lang, string? include_unofficial = null) => GetFunc([lang, include_unofficial], ':');

    [LuaType($"{LuaTypeNames.table}<{nameof(EnumsTable.EpisodeType)}, {LuaTypeNames.integer}>")]
    [LuaDescription("Count of episodes by type")]
    public string episodecounts => Get();

    [LuaType($"{LuaTypeNames.Relation}[]")]
    [LuaDescription("Related anime entries, not populated for nested Anime entries")]
    public ArrayTable<RelationTable> relations => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.@string}[]")]
    [LuaDescription("List of studios that produced the anime")]
    public string studios => Get();

    public string _classid => Get();
    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";
}
