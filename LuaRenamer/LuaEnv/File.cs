// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class File : Table
{
    public string name => Get();
    public string extension => Get();
    public string path => Get();
    public string size => Get();
    public ImportFolder importfolder => new() { Fn = Get() };
    public string earliestname => Get();
    public Media media => new() { Fn = Get() };
    public AniDb anidb => new() { Fn = Get() };
    public Hashes hashes => new() { Fn = Get() };
}
