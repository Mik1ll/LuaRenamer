// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class TmdbTable : Table
{
    [LuaType("TmdbMovie[]")]
    [LuaDescription("List of TMDB movies")]
    public ArrayTable<TmdbMovieTable> movies => new() { Fn = Get() };

    [LuaType("TmdbShow[]")]
    [LuaDescription("List of TMDB shows")]
    public ArrayTable<TmdbShowTable> shows => new() { Fn = Get() };

    [LuaType("TmdbEpisode[]")]
    [LuaDescription("List of TMDB episodes")]
    public ArrayTable<TmdbEpisodeTable> episodes => new() { Fn = Get() };
}
