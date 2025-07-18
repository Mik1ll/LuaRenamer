// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class AniDbMediaTable : Table
{
    [LuaType("Language[]")]
    [LuaDescription("List of subtitle languages available in the release")]
    public string sublanguages => Get();

    [LuaType("Language[]")]
    [LuaDescription("List of audio languages available in the release")]
    public string dublanguages => Get();
}
