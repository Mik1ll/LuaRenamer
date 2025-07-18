// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class ReleaseGroupTable : Table
{
    [LuaType("string")]
    [LuaDescription("Full name of the release group")]
    public string name => Get();

    [LuaType("string")]
    [LuaDescription("Abbreviated name or acronym of the release group")]
    public string shortname => Get();
}
