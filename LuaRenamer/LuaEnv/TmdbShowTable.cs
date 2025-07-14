// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class TmdbShowTable : Table
{
    public string id => Get();
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };
    public string defaultname => Get();
    public string preferredname => Get();
    public string rating => Get();
    public string restricted => Get();
    public string studios => Get();
    public string episodecount => Get();
    public string getname(string lang) => GetFunc([lang], ':');
}
