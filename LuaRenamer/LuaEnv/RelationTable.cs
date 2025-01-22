// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class RelationTable : Table
{
    public AnimeTable anime => new() { Fn = Get() };
    public string type => Get();
}
