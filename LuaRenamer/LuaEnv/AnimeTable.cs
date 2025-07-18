// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class AnimeTable : Table
{
    [LuaType("DateTime", Nillable = true)]
    [LuaDescription("First air date of the anime")]
    public DateTimeTable airdate => new() { Fn = Get() };

    [LuaType("DateTime", Nillable = true)]
    [LuaDescription("Last air date of the anime")]
    public DateTimeTable enddate => new() { Fn = Get() };

    [LuaType("number")]
    [LuaDescription("Average rating of the anime")]
    public string rating => Get();

    [LuaType("boolean")]
    [LuaDescription("Whether the anime is age-restricted")]
    public string restricted => Get();

    [LuaType("AnimeType")]
    [LuaDescription("Type of the anime (Movie, TVSeries, etc.)")]
    public string type => Get();

    [LuaType("string")]
    [LuaDescription("The preferred title for the anime")]
    public string preferredname => Get();

    [LuaType("string")]
    [LuaDescription("The default title for the anime")]
    public string defaultname => Get();

    [LuaType("integer")]
    [LuaDescription("Unique identifier for the anime")]
    public string id => Get();

    [LuaType("Title[]")]
    [LuaDescription("All available titles for the anime")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType("function")]
    [LuaDescription("Get the anime title in the specified language")]
    [LuaParameter("lang", "Language", "The language to get the title in")]
    [LuaParameter("include_unofficial", "boolean", "Whether to include unofficial titles")]
    [LuaReturnType("string", Nillable = true)]
    public string getname(string lang, string? include_unofficial = null) => GetFunc([lang, include_unofficial], ':');

    [LuaType("table<EpisodeType, integer>")]
    [LuaDescription("Count of episodes by type")]
    public string episodecounts => Get();

    [LuaType("Relation[]")]
    [LuaDescription("Related anime entries")]
    public ArrayTable<RelationTable> relations => new() { Fn = Get() };

    [LuaType("string[]")]
    [LuaDescription("List of studios that produced the anime")]
    public string studios => Get();

    [LuaType("string")]
    [LuaDescription("Class identifier for cross-API linking")]
    public string _classid => Get();
    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";
}
