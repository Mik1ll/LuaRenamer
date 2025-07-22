// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Hashes)]
public class HashesTable : Table
{
    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "CRC32 hash of the file")]
    public string crc => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "MD5 hash of the file")]
    public string md5 => Get();

    [LuaType(LuaTypeNames.@string, "ED2K hash of the file")]
    public string ed2k => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "SHA1 hash of the file")]
    public string sha1 => Get();
}
