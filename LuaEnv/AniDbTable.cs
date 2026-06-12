// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class AniDbTable : LuaTableWriter
{
    public AniDbTable(LuaTable t) : base(t) { }

    [LuaField("AniDB file ID")]
    public required long id { init => Set(value); }

    [LuaField("Whether the release is censored")]
    public required bool? censored { init => Set(value); }

    [LuaField("Source media of the release e.g. DVD, BD, Web, etc.")]
    public required string source { init => Set(value); }

    [LuaField("Version number of the release")]
    public required long version { init => Set(value); }

    [LuaField("Release date of the file")]
    public required LuaRef<DateTimeTable>? releasedate { init => Set(value?.Table); }

    [LuaField("Description or notes about the release")]
    public required string? description { init => Set(value); }

    [LuaField("Information about the release group")]
    public required LuaRef<ReleaseGroupTable>? releasegroup { init => Set(value?.Table); }

    [LuaField("Media information from AniDB")]
    public required LuaRef<AniDbMediaTable> media { init => Set(value.Table); }

    public static implicit operator LuaRef<AniDbTable>(AniDbTable t) => new(t._t);
}
