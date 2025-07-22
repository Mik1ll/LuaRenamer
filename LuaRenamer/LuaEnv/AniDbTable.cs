// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.AniDb)]
public class AniDbTable : Table
{
    [LuaType(LuaTypeNames.integer, "AniDB file ID")]
    public string id => Get();

    [LuaType(LuaTypeNames.boolean, "Whether the release is censored")]
    public string censored => Get();

    [LuaType(LuaTypeNames.@string, "Source of the release (e.g., 'DVD', 'BD', etc.)")]
    public string source => Get();

    [LuaType(LuaTypeNames.integer, "Version number of the release")]
    public string version => Get();

    [LuaType(LuaTypeNames.DateTime, "Release date of the file")]
    public DateTimeTable releasedate => new() { Fn = Get() };

    [LuaType(LuaTypeNames.@string, "Description or notes about the release")]
    public string description => Get();

    [LuaType($"{LuaTypeNames.ReleaseGroup}|{LuaTypeNames.nil}", "Information about the release group")]
    public ReleaseGroupTable releasegroup => new() { Fn = Get() };

    [LuaType(LuaTypeNames.AniDbMedia, "Media information from AniDB")]
    public AniDbMediaTable media => new() { Fn = Get() };
}
