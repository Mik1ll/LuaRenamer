// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Audio)]
public class AudioTable : Table
{
    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Audio compression mode")]
    public string compressionmode => Get();

    [LuaType(LuaTypeNames.number)]
    [LuaDescription("Number of audio channels")]
    public string channels => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Audio sampling rate in Hz")]
    public string samplingrate => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Audio codec name")]
    public string codec => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Audio track language")]
    public string language => Get();

    [LuaType(LuaTypeNames.@string, Nillable = true)]
    [LuaDescription("Audio track title or name")]
    public string title => Get();
}
