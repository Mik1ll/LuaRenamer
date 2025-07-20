// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Title)]
public class TitleTable : Table
{
    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("The title text")]
    public string name => Get();

    [LuaType(nameof(EnumsTable.Language))]
    [LuaDescription("Language of the title")]
    public string language => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("ISO language code")]
    public string languagecode => Get();

    [LuaType(nameof(EnumsTable.TitleType))]
    [LuaDescription("Type of title (Main, Official, etc.)")]
    public string type => Get();
}
