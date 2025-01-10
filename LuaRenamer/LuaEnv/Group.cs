// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Group : Table
{
    public string name => Get();
    public string mainanime => Get();
    public string animes => Get();
}
