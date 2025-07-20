// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Relation)]
public class RelationTable : Table
{
    [LuaType(LuaTypeNames.Anime)]
    [LuaDescription("The related anime")]
    public AnimeTable anime => new() { Fn = Get() };

    [LuaType(nameof(EnumsTable.RelationType))]
    [LuaDescription("Type of relation between the anime")]
    public string type => Get();
}
