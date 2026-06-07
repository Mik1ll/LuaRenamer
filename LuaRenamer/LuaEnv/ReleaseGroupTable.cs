// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.ReleaseGroup)]
public partial class ReleaseGroupTable : LuaTableWriter
{
    internal ReleaseGroupTable(LuaTable t) : base(t) { }

    [LuaField("Full name of the release group")]
    public required string name { init => Set(value); }

    [LuaField("Abbreviated name or acronym of the release group")]
    public required string shortname { init => Set(value); }

    public static implicit operator LuaRef<ReleaseGroupTable>(ReleaseGroupTable t) => new(t._t);
}
