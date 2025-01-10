// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Relation : Table
{
    public Anime anime => new() { Fn = Get() };
    public string type => Get();
}
