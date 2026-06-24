// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.LuaEnv;

public partial class ImportFolderTable : LuaTableWriter
{
    public ImportFolderTable(LuaTable t) : base(t) { }

    [LuaField("The Shoko import folder ID")]
    public required long id { init => Set(value); }

    [LuaField("Name of the import folder")]
    public required string name { init => Set(value); }

    [LuaField("File system path to the import folder")]
    public required string location { init => Set(value); }

    [LuaField("Type of the import folder")]
    public required DropFolderType type { init => Set(value.ToString()); }

    public static implicit operator LuaRef<ImportFolderTable>(ImportFolderTable t) => new(t._t);
}
