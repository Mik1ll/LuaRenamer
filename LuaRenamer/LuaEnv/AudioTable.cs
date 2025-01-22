// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class AudioTable : Table
{
    public string compressionmode => Get();
    public string channels => Get();
    public string samplingrate => Get();
    public string codec => Get();
    public string language => Get();
    public string title => Get();
}
