// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class MediaTable : LuaTableWriter
{
    public MediaTable(LuaTable t) : base(t) { }

    [LuaField("Whether the media file contains chapters")]
    public required bool chaptered { init => Set(value); }

    [LuaField("Duration of the media in seconds")]
    public required long duration { init => Set(value); }

    [LuaField("Overall bitrate of the media file")]
    public required long bitrate { init => Set(value); }

    [LuaField("List of subtitle languages")]
    public required LuaArray<string> sublanguages { init => Set(value.Table); }

    [LuaField("List of audio tracks")]
    public required LuaArray<LuaRef<AudioTable>> audio { init => Set(value.Table); }

    [LuaField("Video stream information")]
    public required LuaRef<VideoTable>? video { init => Set(value?.Table); }

    public static implicit operator LuaRef<MediaTable>(MediaTable t) => new(t._t);
}
