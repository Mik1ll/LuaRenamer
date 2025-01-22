// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class VideoTable : Table
{
    public string height => Get();
    public string width => Get();
    public string codec => Get();
    public string res => Get();
    public string bitrate => Get();
    public string bitdepth => Get();
    public string framerate => Get();
}
