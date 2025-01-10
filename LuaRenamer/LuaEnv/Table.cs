using System.Runtime.CompilerServices;

namespace LuaRenamer.LuaEnv;

public class Table
{
    public string Fn { get; init; } = "";
    public override string ToString() => Fn;
    protected string Get(char sep = '.', [CallerMemberName] string memberName = "") => string.IsNullOrEmpty(Fn) ? memberName : Fn + sep + memberName;
}
