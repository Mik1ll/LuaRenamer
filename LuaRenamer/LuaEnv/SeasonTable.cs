// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Season)]
public partial class SeasonTable : LuaTableWriter
{
    internal SeasonTable(LuaTable t) : base(t) { }

    [LuaField("Season year")]
    public required long year { init => Set(value); }

    [LuaField("Season aired")]
    public required YearlySeason season { init => Set(value.ToString()); }

    public static implicit operator LuaRef<SeasonTable>(SeasonTable t) => new(t._t);
}
