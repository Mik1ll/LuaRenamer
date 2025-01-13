// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class ImportFolder : Table
{
    public string id => Get();
    public string name => Get();
    public string location => Get();
    public string type => Get();
    public string _classid => Get();
    public const string _classidVal = "55138454-4A0D-45EB-8CCE-1CCF00220165";
}
