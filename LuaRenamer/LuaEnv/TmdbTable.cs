// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class TmdbTable : Table
{
    public ArrayTable<TmdbMovieTable> movies => new() { Fn = Get() };
    public ArrayTable<TmdbShowTable> shows => new() { Fn = Get() };
    public ArrayTable<TmdbEpisodeTable> episodes => new() { Fn = Get() };
}
