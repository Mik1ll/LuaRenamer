// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Audio)]
public class AudioTable : Table
{
    [LuaType(LuaTypeNames.@string, "Audio compression mode")]
    public string compressionmode => Get();

    [LuaType(LuaTypeNames.number, "Number of audio channels, may have decimal part '.1'")]
    public string channels => Get();

    [LuaType(LuaTypeNames.integer, "Audio sampling rate in Hz")]
    public string samplingrate => Get();

    [LuaType(LuaTypeNames.@string, "Audio codec name")]
    public string codec => Get();

    [LuaType(LuaTypeNames.@string, "Audio track language")]
    public string language => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "Audio track title or name")]
    public string title => Get();
}
