// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class EpisodeTable : Table
{
    public string duration => Get();
    public string number => Get();
    public string type => Get();
    public DateTable airdate => new() { Fn = Get() };
    public string animeid => Get();
    public string id => Get();
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };
    public string getname(string lang, string? include_unofficial = null) => GetFunc([lang, include_unofficial], ':');
    public string prefix => Get();
    public string _classid => Get();
    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";
}
