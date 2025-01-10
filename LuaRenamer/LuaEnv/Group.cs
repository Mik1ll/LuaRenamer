// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Group : Table
{
    public string name => Get();
    public Anime mainanime => new() { Fn = Get() };
    public Array<Anime> animes => new() { Fn = Get() };
}
