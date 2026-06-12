// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class TmdbTable : LuaTableWriter
{
    internal TmdbTable(LuaTable t) : base(t) { }

    [LuaField("List of TMDB movies related to the file")]
    public required LuaArray<LuaRef<TmdbMovieTable>> movies { init => Set(value.Table); }

    [LuaField("List of TMDB shows related to the file")]
    public required LuaArray<LuaRef<TmdbShowTable>> shows { init => Set(value.Table); }

    [LuaField("List of TMDB episodes related to the file")]
    public required LuaArray<LuaRef<TmdbEpisodeTable>> episodes { init => Set(value.Table); }

    public static implicit operator LuaRef<TmdbTable>(TmdbTable t) => new(t._t);
}
