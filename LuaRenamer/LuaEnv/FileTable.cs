// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class FileTable : Table
{
    [LuaType("string")]
    [LuaDescription("The name of the file without extension")]
    public string name => Get();

    [LuaType("string")]
    [LuaDescription("The file extension including the dot")]
    public string extension => Get();

    [LuaType("string")]
    [LuaDescription("The full path to the file")]
    public string path => Get();

    [LuaType("integer")]
    [LuaDescription("The file size in bytes")]
    public string size => Get();

    [LuaType("ImportFolder")]
    [LuaDescription("The import folder containing this file")]
    public ImportFolderTable importfolder => new() { Fn = Get() };

    [LuaType("string", Nillable = true)]
    [LuaDescription("The earliest known name of the file")]
    public string earliestname => Get();

    [LuaType("Media", Nillable = true)]
    [LuaDescription("Media information for the file")]
    public MediaTable media => new() { Fn = Get() };

    [LuaType("AniDb", Nillable = true)]
    [LuaDescription("AniDB information for the file")]
    public AniDbTable anidb => new() { Fn = Get() };

    [LuaType("Hashes")]
    [LuaDescription("File hashes including CRC, MD5, ED2K and SHA1")]
    public HashesTable hashes => new() { Fn = Get() };
}
