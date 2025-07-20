// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Hashes)]
public class HashesTable : Table
{
    [LuaType(LuaTypeNames.@string, Nillable = true)]
    [LuaDescription("CRC32 hash of the file")]
    public string crc => Get();

    [LuaType(LuaTypeNames.@string, Nillable = true)]
    [LuaDescription("MD5 hash of the file")]
    public string md5 => Get();

    [LuaType(LuaTypeNames.@string)]
    [LuaDescription("ED2K hash of the file")]
    public string ed2k => Get();

    [LuaType(LuaTypeNames.@string, Nillable = true)]
    [LuaDescription("SHA1 hash of the file")]
    public string sha1 => Get();
}
