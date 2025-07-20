// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Media)]
public class MediaTable : Table
{
    [LuaType(LuaTypeNames.boolean)]
    [LuaDescription("Whether the media file contains chapters")]
    public string chaptered => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Duration of the media in seconds")]
    public string duration => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Overall bitrate of the media file")]
    public string bitrate => Get();

    [LuaType($"{LuaTypeNames.@string}[]")]
    [LuaDescription("List of subtitle languages")]
    public string sublanguages => Get();

    [LuaType($"{LuaTypeNames.Audio}[]")]
    [LuaDescription("List of audio tracks")]
    public ArrayTable<AudioTable> audio => new() { Fn = Get() };

    [LuaType(LuaTypeNames.Video, Nillable = true)]
    [LuaDescription("Video stream information")]
    public VideoTable video => new() { Fn = Get() };
}
