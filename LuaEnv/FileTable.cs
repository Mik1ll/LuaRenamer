// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class FileTable : LuaTableWriter
{
    internal FileTable(LuaTable t) : base(t) { }

    [LuaField("The name of the file without extension")]
    public required string name { init => Set(value); }

    [LuaField("The file extension including the dot")]
    public required string extension { init => Set(value); }

    [LuaField("The full path to the file")]
    public required string path { init => Set(value); }

    [LuaField("The file size in bytes")]
    public required long size { init => Set(value); }

    [LuaField("The import folder containing this file")]
    public required LuaRef<ImportFolderTable> importfolder { init => Set(value.Table); }

    [LuaField("The earliest known name of the file")]
    public required string? earliestname { init => Set(value); }

    [LuaField("Media information (via MediaInfo) for the file")]
    public required LuaRef<MediaTable>? media { init => Set(value?.Table); }

    [LuaField("AniDB information for the file")]
    public required LuaRef<AniDbTable>? anidb { init => Set(value?.Table); }

    [LuaField("File hashes")]
    public required LuaRef<HashesTable> hashes { init => Set(value.Table); }

    public static implicit operator LuaRef<FileTable>(FileTable t) => new(t._t);
}
