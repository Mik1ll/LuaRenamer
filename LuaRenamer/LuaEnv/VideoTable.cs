// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class VideoTable : Table
{
    [LuaType("integer")]
    [LuaDescription("Video height in pixels")]
    public string height => Get();

    [LuaType("integer")]
    [LuaDescription("Video width in pixels")]
    public string width => Get();

    [LuaType("string")]
    [LuaDescription("Video codec name")]
    public string codec => Get();

    [LuaType("string")]
    [LuaDescription("Resolution string (e.g., '1080p', '720p')")]
    public string res => Get();

    [LuaType("integer")]
    [LuaDescription("Video bitrate in bits per second")]
    public string bitrate => Get();

    [LuaType("integer")]
    [LuaDescription("Color depth in bits per channel")]
    public string bitdepth => Get();

    [LuaType("number")]
    [LuaDescription("Frame rate in frames per second")]
    public string framerate => Get();
}
