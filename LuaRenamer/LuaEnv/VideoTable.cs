// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Video)]
public class VideoTable : Table
{
    [LuaType(LuaTypeNames.integer, "Video height in pixels")]
    public string height => Get();

    [LuaType(LuaTypeNames.integer, "Video width in pixels")]
    public string width => Get();

    [LuaType(LuaTypeNames.@string, "Video codec name")]
    public string codec => Get();

    [LuaType(LuaTypeNames.@string, "Resolution string (e.g., '1080p', '720p')")]
    public string res => Get();

    [LuaType(LuaTypeNames.integer, "Video bitrate in bits per second")]
    public string bitrate => Get();

    [LuaType(LuaTypeNames.integer, "Color depth in bits per channel")]
    public string bitdepth => Get();

    [LuaType(LuaTypeNames.number, "Frame rate in frames per second")]
    public string framerate => Get();
}
