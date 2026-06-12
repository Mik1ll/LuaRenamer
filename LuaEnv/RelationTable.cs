// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

public partial class RelationTable : LuaTableWriter
{
    public RelationTable(LuaTable t) : base(t) { }

    [LuaField("The related anime")]
    public required LuaRef<AnimeTable> anime { init => Set(value.Table); }

    [LuaField("Type of relation between the anime")]
    public required RelationType type { init => Set(value.ToString()); }

    public static implicit operator LuaRef<RelationTable>(RelationTable t) => new(t._t);
}
