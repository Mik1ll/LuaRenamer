// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class FileTable : Table
{
    public string name => Get();
    public string extension => Get();
    public string path => Get();
    public string size => Get();
    public ImportFolderTable importfolder => new() { Fn = Get() };
    public string earliestname => Get();
    public MediaTable media => new() { Fn = Get() };
    public AniDbTable anidb => new() { Fn = Get() };
    public HashesTable hashes => new() { Fn = Get() };
}
