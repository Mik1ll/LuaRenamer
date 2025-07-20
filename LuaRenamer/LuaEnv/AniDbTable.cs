// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.AniDb)]
public class AniDbTable : Table
{
    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("AniDB file ID")]
    public string id => Get();

    [LuaType(LuaTypeNames.boolean)]
    [LuaDescription("Whether the release is censored")]
    public string censored => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Source of the release (e.g., 'DVD', 'BD', etc.)")]
    public string source => Get();

    [LuaType(LuaTypeNames.integer)]
    [LuaDescription("Version number of the release")]
    public string version => Get();

    [LuaType(LuaTypeNames.DateTime)]
    [LuaDescription("Release date of the file")]
    public DateTimeTable releasedate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("Description or notes about the release")]
    public string description => Get();

    [LuaType(LuaTypeNames.ReleaseGroup, Nillable = true)]
    [LuaDescription("Information about the release group")]
    public ReleaseGroupTable releasegroup => new() { Fn = Get() };

    [LuaType(LuaTypeNames.AniDbMedia)]
    [LuaDescription("Media information from AniDB")]
    public AniDbMediaTable media => new() { Fn = Get() };
}
