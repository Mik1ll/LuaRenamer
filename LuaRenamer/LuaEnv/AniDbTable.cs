// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class AniDbTable : Table
{
    [LuaType("integer")]
    [LuaDescription("AniDB file ID")]
    public string id => Get();

    [LuaType("boolean")]
    [LuaDescription("Whether the release is censored")]
    public string censored => Get();

    [LuaType("string")]
    [LuaDescription("Source of the release (e.g., 'DVD', 'BD', etc.)")]
    public string source => Get();

    [LuaType("integer")]
    [LuaDescription("Version number of the release")]
    public string version => Get();

    [LuaType("DateTime")]
    [LuaDescription("Release date of the file")]
    public DateTimeTable releasedate => new() { Fn = Get() };

    [LuaType("string")]
    [LuaDescription("Description or notes about the release")]
    public string description => Get();

    [LuaType("ReleaseGroup", Nillable = true)]
    [LuaDescription("Information about the release group")]
    public ReleaseGroupTable releasegroup => new() { Fn = Get() };

    [LuaType("AniDbMedia")]
    [LuaDescription("Media information from AniDB")]
    public AniDbMediaTable media => new() { Fn = Get() };
}
