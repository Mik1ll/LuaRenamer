// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class TitleTable : Table
{
    [LuaType("string")]
    [LuaDescription("The title text")]
    public string name => Get();

    [LuaType("Language")]
    [LuaDescription("Language of the title")]
    public string language => Get();

    [LuaType("string")]
    [LuaDescription("ISO language code")]
    public string languagecode => Get();

    [LuaType("TitleType")]
    [LuaDescription("Type of title (Main, Official, etc.)")]
    public string type => Get();
}
