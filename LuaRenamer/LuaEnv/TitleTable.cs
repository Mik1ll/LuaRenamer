// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Title)]
public class TitleTable : Table
{
    [LuaType(LuaTypeNames.@string, "The title text")]
    public string name => Get();

    [LuaType(nameof(EnumsTable.Language), "Language of the title")]
    public string language => Get();

    [LuaType(LuaTypeNames.@string, "ISO language code")]
    public string languagecode => Get();

    [LuaType(nameof(EnumsTable.TitleType), "Type of title (Main, Official, etc.)")]
    public string type => Get();
}
