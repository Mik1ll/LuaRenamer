// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class AniDbTable : Table
{
    public string id => Get();
    public string censored => Get();
    public string source => Get();
    public string version => Get();
    public DateTable releasedate => new() { Fn = Get() };
    public string description => Get();
    public ReleaseGroupTable releasegroup => new() { Fn = Get() };
    public AniDbMediaTable media => new() { Fn = Get() };
}
