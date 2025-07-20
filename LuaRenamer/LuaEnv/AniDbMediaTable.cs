// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.AniDbMedia)]
public class AniDbMediaTable : Table
{
    [LuaType($"{nameof(EnumsTable.Language)}[]")]
    [LuaDescription("List of subtitle languages available in the release")]
    public string sublanguages => Get();

    [LuaType($"{nameof(EnumsTable.Language)}[]")]
    [LuaDescription("List of audio languages available in the release")]
    public string dublanguages => Get();
}
