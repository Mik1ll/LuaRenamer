// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class AudioTable : Table
{
    [LuaType("string")]
    [LuaDescription("Audio compression mode")]
    public string compressionmode => Get();

    [LuaType("number")]
    [LuaDescription("Number of audio channels")]
    public string channels => Get();

    [LuaType("integer")]
    [LuaDescription("Audio sampling rate in Hz")]
    public string samplingrate => Get();

    [LuaType("string")]
    [LuaDescription("Audio codec name")]
    public string codec => Get();

    [LuaType("string")]
    [LuaDescription("Audio track language")]
    public string language => Get();

    [LuaType("string", Nillable = true)]
    [LuaDescription("Audio track title or name")]
    public string title => Get();
}
