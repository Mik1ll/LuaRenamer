// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class AniDb : Table
{
    public string id => Get();
    public string censored => Get();
    public string source => Get();
    public string version => Get();
    public string releasedate => Get();
    public string description => Get();
    public ReleaseGroup releasegroup => new() { Fn = Get() };
    public AniDbMedia media => new() { Fn = Get() };
}
