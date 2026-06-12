// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class HashesTable : LuaTableWriter
{
    internal HashesTable(LuaTable t) : base(t) { }

    [LuaField("CRC32 hash of the file")]
    public required string? crc { init => Set(value); }

    [LuaField("MD5 hash of the file")]
    public required string? md5 { init => Set(value); }

    [LuaField("ED2K hash of the file")]
    public required string ed2k { init => Set(value); }

    [LuaField("SHA1 hash of the file")]
    public required string? sha1 { init => Set(value); }

    public static implicit operator LuaRef<HashesTable>(HashesTable t) => new(t._t);
}
