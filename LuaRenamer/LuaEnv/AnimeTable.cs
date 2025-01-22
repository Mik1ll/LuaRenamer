// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class AnimeTable : Table
{
    public DateTable airdate => new() { Fn = Get() };
    public DateTable enddate => new() { Fn = Get() };
    public string rating => Get();
    public string restricted => Get();
    public string type => Get();
    public string preferredname => Get();
    public string defaultname => Get();
    public string id => Get();
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };
    public string getname => Get(':');
    public string episodecounts => Get();
    public ArrayTable<RelationTable> relations => new() { Fn = Get() };
    public string _classid => Get();
    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";
}
