// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class MediaTable : Table
{
    public string chaptered => Get();
    public string duration => Get();
    public string bitrate => Get();
    public string sublanguages => Get();
    public ArrayTable<AudioTable> audio => new() { Fn = Get() };
    public VideoTable video => new() { Fn = Get() };
}
