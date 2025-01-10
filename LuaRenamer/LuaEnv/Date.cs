// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Date : Table
{
    public string year => Get();
    public string month => Get();
    public string day => Get();
    public string yday => Get();
    public string wday => Get();
    public string hour => Get();
    public string min => Get();
    public string sec => Get();
    public string isdst => Get();
}
