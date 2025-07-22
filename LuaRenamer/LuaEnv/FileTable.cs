// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.File)]
public class FileTable : Table
{
    [LuaType(LuaTypeNames.@string, "The name of the file without extension")]
    public string name => Get();

    [LuaType(LuaTypeNames.@string, "The file extension including the dot")]
    public string extension => Get();

    [LuaType(LuaTypeNames.@string, "The full path to the file")]
    public string path => Get();

    [LuaType(LuaTypeNames.integer, "The file size in bytes")]
    public string size => Get();

    [LuaType(LuaTypeNames.ImportFolder, "The import folder containing this file")]
    public ImportFolderTable importfolder => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "The earliest known name of the file")]
    public string earliestname => Get();

    [LuaType($"{LuaTypeNames.Media}|{LuaTypeNames.nil}", "Media information (via MediaInfo) for the file")]
    public MediaTable media => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.AniDb}|{LuaTypeNames.nil}", "AniDB information for the file")]
    public AniDbTable anidb => new() { Fn = Get() };

    [LuaType(LuaTypeNames.Hashes, "File hashes")]
    public HashesTable hashes => new() { Fn = Get() };
}
