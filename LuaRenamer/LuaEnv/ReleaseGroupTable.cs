// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.ReleaseGroup)]
public class ReleaseGroupTable : Table
{
    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Full name of the release group")]
    public string name => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Abbreviated name or acronym of the release group")]
    public string shortname => Get();
}
