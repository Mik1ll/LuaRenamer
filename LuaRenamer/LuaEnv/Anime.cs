// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Anime : Table
{
    public string airdate => Get();
    public string enddate => Get();
    public string rating => Get();
    public string restricted => Get();
    public string type => Get();
    public string preferredname => Get();
    public string defaultname => Get();
    public string id => Get();
    public Array<Title> titles => new() { Fn = Get() };
    public string getname => Get(':');
    public string episodecounts => Get();
    public Array<Relation> relations => new() { Fn = Get() };
    public string _classid => Get();
    public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";
}
