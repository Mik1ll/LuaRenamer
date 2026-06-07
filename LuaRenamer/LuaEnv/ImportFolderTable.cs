// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.ImportFolder)]
public partial class ImportFolderTable : LuaTableWriter
{
    internal ImportFolderTable(LuaTable t) : base(t, _classidVal) { }

    [LuaField("The Shoko import folder ID")]
    public required long id { init => Set(value); }

    [LuaField("Name of the import folder")]
    public required string name { init => Set(value); }

    [LuaField("File system path to the import folder")]
    public required string location { init => Set(value); }

    [LuaField("Type of the import folder")]
    public required DropFolderType type { init => Set(value.ToString()); }

    public const string _classidVal = "55138454-4A0D-45EB-8CCE-1CCF00220165";

    public static implicit operator LuaRef<ImportFolderTable>(ImportFolderTable t) => new(t._t);
}
