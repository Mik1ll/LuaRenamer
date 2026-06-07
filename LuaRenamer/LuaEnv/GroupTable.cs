// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Group)]
public partial class GroupTable : LuaTableWriter
{
    internal GroupTable(LuaTable t) : base(t) { }

    [LuaField("The name of the group")]
    public required string? name { init => Set(value); }

    [LuaField("The main anime in the group")]
    public required LuaRef<AnimeTable> mainanime { init => Set(value.Table); }

    [LuaField("All animes in the group")]
    public required LuaArray<LuaRef<AnimeTable>> animes { init => Set(value.Table); }

    public static implicit operator LuaRef<GroupTable>(GroupTable t) => new(t._t);
}
