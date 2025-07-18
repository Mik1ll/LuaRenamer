// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class EpisodeTable : Table
{
    [LuaType("integer")]
    [LuaDescription("Duration of the episode in seconds")]
    public string duration => Get();

    [LuaType("integer")]
    [LuaDescription("Episode number")]
    public string number => Get();

    [LuaType("EpisodeType")]
    [LuaDescription("Type of episode (Episode, Special, etc.)")]
    public string type => Get();

    [LuaType("DateTime", Nillable = true)]
    [LuaDescription("Air date of the episode")]
    public DateTimeTable airdate => new() { Fn = Get() };

    [LuaType("integer")]
    [LuaDescription("ID of the anime this episode belongs to")]
    public string animeid => Get();

    [LuaType("integer")]
    [LuaDescription("Unique identifier for the episode")]
    public string id => Get();

    [LuaType("Title[]")]
    [LuaDescription("All available titles for the episode")]
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };

    [LuaType("function")]
    [LuaDescription("Get the episode title in the specified language")]
    [LuaParameter("lang", "Language", "The language to get the title in")]
    [LuaReturnType("string", Nillable = true)]
    public string getname(string lang) => GetFunc([lang], ':');

    [LuaType("string")]
    [LuaDescription("Episode number prefix (e.g., '', 'C', 'S', 'T', 'P', 'O')")]
    public string prefix => Get();

    [LuaType("string")]
    [LuaDescription("Class identifier for cross-API linking")]
    public string _classid => Get();
    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";
}
