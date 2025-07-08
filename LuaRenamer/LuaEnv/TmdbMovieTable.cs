// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class TmdbMovieTable : Table
{
    public string id => Get();
    public ArrayTable<TitleTable> titles => new() { Fn = Get() };
    public string defaultname => Get();
    public string preferredname => Get();
    public string rating => Get();
    public string restricted => Get();
    public string studios => Get();
}
