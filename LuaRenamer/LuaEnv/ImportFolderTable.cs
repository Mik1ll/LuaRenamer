// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class ImportFolderTable : Table
{
    [LuaType("integer")]
    [LuaDescription("Unique identifier for the import folder")]
    public string id => Get();

    [LuaType("string")]
    [LuaDescription("Name of the import folder")]
    public string name => Get();

    [LuaType("string")]
    [LuaDescription("File system path to the import folder")]
    public string location => Get();

    [LuaType("ImportFolderType")]
    [LuaDescription("Type of the import folder (Source, Destination, etc.)")]
    public string type => Get();

    public string _classid => Get();
    public const string _classidVal = "55138454-4A0D-45EB-8CCE-1CCF00220165";
}
