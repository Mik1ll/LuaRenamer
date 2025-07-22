// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Episode)]
public class EpisodeTable : Table
{
    [LuaType(LuaTypeNames.integer, "Duration of the episode in seconds")]
    public string duration => Get();

    [LuaType(LuaTypeNames.integer, "Episode number")]
    public string number => Get();

    [LuaType(nameof(EnumsTable.EpisodeType), "Type of episode (Episode, Special, etc.)")]
    public string type => Get();

    [LuaType($"{LuaTypeNames.DateTime}|{LuaTypeNames.nil}", "Air date of the episode")]
    public DateTimeTable airdate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.integer, "ID of the anime this episode belongs to")]
    public string animeid => Get();

    [LuaType(LuaTypeNames.integer, "Unique identifier for the episode")]
    public string id => Get();

    [LuaType($"{LuaTypeNames.Title}[]", "All available titles for the episode")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType(LuaTypeNames.function, "Get the episode title in the specified language")]
    [LuaParameter("lang", nameof(EnumsTable.Language), "The language to get the title in")]
    [LuaReturnType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}")]
    public string getname(string lang) => GetFunc([lang], ':');

    [LuaType(LuaTypeNames.@string, "Episode number prefix (e.g., '', 'C', 'S', 'T', 'P', 'O')")]
    public string prefix => Get();

    public string _classid => Get();
    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";
}
