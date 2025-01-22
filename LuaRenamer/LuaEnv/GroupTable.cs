// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class GroupTable : Table
{
    public string name => Get();
    public AnimeTable mainanime => new() { Fn = Get() };
    public ArrayTable<AnimeTable> animes => new() { Fn = Get() };
}
