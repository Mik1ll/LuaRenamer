// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class RelationTable : Table
{
    [LuaType("Anime")]
    [LuaDescription("The related anime")]
    public AnimeTable anime => new() { Fn = Get() };

    [LuaType("RelationType")]
    [LuaDescription("Type of relation between the anime")]
    public string type => Get();
}
