// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class AudioTable : LuaTableWriter
{
    public AudioTable(LuaTable t) : base(t) { }

    [LuaField("Audio compression mode")]
    public required string compressionmode { init => Set(value); }

    [LuaField("Number of audio channels, may have decimal part '.1'")]
    public required double channels { init => Set(value); }

    [LuaField("Audio sampling rate in Hz")]
    public required long samplingrate { init => Set(value); }

    [LuaField("Audio codec name")]
    public required string codec { init => Set(value); }

    [LuaField("Audio track language")]
    public required string language { init => Set(value); }

    [LuaField("Audio track title or name")]
    public required string? title { init => Set(value); }

    public static implicit operator LuaRef<AudioTable>(AudioTable t) => new(t._t);
}
