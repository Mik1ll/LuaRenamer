// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class VideoTable : LuaTableWriter
{
    internal VideoTable(LuaTable t) : base(t) { }

    [LuaField("Video height in pixels")]
    public required long height { init => Set(value); }

    [LuaField("Video width in pixels")]
    public required long width { init => Set(value); }

    [LuaField("Video codec name")]
    public required string codec { init => Set(value); }

    [LuaField("Resolution string e.g. '1080p', '720p', etc.")]
    public required string res { init => Set(value); }

    [LuaField("Video bitrate in bits per second")]
    public required long bitrate { init => Set(value); }

    [LuaField("Color depth in bits per channel")]
    public required long bitdepth { init => Set(value); }

    [LuaField("Frame rate in frames per second")]
    public required double framerate { init => Set(value); }

    public static implicit operator LuaRef<VideoTable>(VideoTable t) => new(t._t);
}
