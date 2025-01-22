// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class HashesTable : Table
{
    public string crc => Get();
    public string md5 => Get();
    public string ed2k => Get();
    public string sha1 => Get();
}
