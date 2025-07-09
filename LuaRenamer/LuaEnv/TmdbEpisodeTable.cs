// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class TmdbEpisodeTable : Table
{
    public string id => Get();
    public string showid => Get();
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };
    public string defaultname => Get();
    public string preferredname => Get();
    public string type => Get();
    public string number => Get();
    public string seasonnumber => Get();
    public string getname => Get(':');
}
