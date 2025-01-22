// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class TitleTable : Table
{
    public string name => Get();
    public string language => Get();
    public string languagecode => Get();
    public string type => Get();
}
