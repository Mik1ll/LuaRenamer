// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Episode : Table
{
    public string duration => Get();
    public string number => Get();
    public string type => Get();
    public string airdate => Get();
    public string animeid => Get();
    public string id => Get();
    public Array<Title> titles => new() { Fn = Get() };
    public string getname => Get(':');
    public string prefix => Get();
    public string _classid => Get();
    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";
}
