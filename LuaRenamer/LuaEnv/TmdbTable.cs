// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Tmdb)]
public class TmdbTable : Table
{
    [LuaType($"{LuaTypeNames.TmdbMovie}[]", "List of TMDB movies")]
    public ArrayTable<TmdbMovieTable> movies => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.TmdbShow}[]", "List of TMDB shows")]
    public ArrayTable<TmdbShowTable> shows => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.TmdbEpisode}[]", "List of TMDB episodes")]
    public ArrayTable<TmdbEpisodeTable> episodes => new() { Fn = Get() };
}
