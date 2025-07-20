// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Video)]
public class VideoTable : Table
{
    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Video height in pixels")]
    public string height => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Video width in pixels")]
    public string width => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Video codec name")]
    public string codec => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Resolution string (e.g., '1080p', '720p')")]
    public string res => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Video bitrate in bits per second")]
    public string bitrate => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Color depth in bits per channel")]
    public string bitdepth => Get();

    [LuaType(LuaTypeNames.number)]
    [LuaDescription("Frame rate in frames per second")]
    public string framerate => Get();
}
