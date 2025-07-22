// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.ImportFolder)]
public class ImportFolderTable : Table
{
    [LuaType(LuaTypeNames.integer, "Unique identifier for the import folder")]
    public string id => Get();

    [LuaType(LuaTypeNames.@string, "Name of the import folder")]
    public string name => Get();

    [LuaType(LuaTypeNames.@string, "File system path to the import folder")]
    public string location => Get();

    [LuaType(nameof(EnumsTable.ImportFolderType), "Type of the import folder (Source, Destination, etc.)")]
    public string type => Get();

    public string _classid => Get();
    public const string _classidVal = "55138454-4A0D-45EB-8CCE-1CCF00220165";
}
