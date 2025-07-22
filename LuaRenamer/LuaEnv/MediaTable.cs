// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Media)]
public class MediaTable : Table
{
    [LuaType(LuaTypeNames.boolean, "Whether the media file contains chapters")]
    public string chaptered => Get();

    [LuaType(LuaTypeNames.integer, "Duration of the media in seconds")]
    public string duration => Get();

    [LuaType(LuaTypeNames.integer, "Overall bitrate of the media file")]
    public string bitrate => Get();

    [LuaType($"{LuaTypeNames.@string}[]", "List of subtitle languages")]
    public string sublanguages => Get();

    [LuaType($"{LuaTypeNames.Audio}[]", "List of audio tracks")]
    public ArrayTable<AudioTable> audio => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Video}|{LuaTypeNames.nil}", "Video stream information")]
    public VideoTable video => new() { Fn = Get() };
}
