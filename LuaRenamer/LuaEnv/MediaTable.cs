// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class MediaTable : Table
{
    [LuaType("boolean")]
    [LuaDescription("Whether the media file contains chapters")]
    public string chaptered => Get();

    [LuaType("integer")]
    [LuaDescription("Duration of the media in seconds")]
    public string duration => Get();

    [LuaType("integer")]
    [LuaDescription("Overall bitrate of the media file")]
    public string bitrate => Get();

    [LuaType("string[]")]
    [LuaDescription("List of subtitle languages")]
    public string sublanguages => Get();

    [LuaType("Audio[]")]
    [LuaDescription("List of audio tracks")]
    public ArrayTable<AudioTable> audio => new() { Fn = Get() };

    [LuaType("Video", Nillable = true)]
    [LuaDescription("Video stream information")]
    public VideoTable video => new() { Fn = Get() };
}
