// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class ReleaseGroup : Table
{
    public string name => Get();
    public string shortname => Get();
}
