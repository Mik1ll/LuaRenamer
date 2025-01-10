// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Media : Table
{
    public string chaptered => Get();
    public string duration => Get();
    public string bitrate => Get();
    public string sublanguages => Get();
    public Array<Audio> audio => new() { Fn = Get() };
    public Video video => new() { Fn = Get() };
}
