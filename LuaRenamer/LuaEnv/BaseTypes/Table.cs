using System.Linq;
using System.Runtime.CompilerServices;

namespace LuaRenamer.LuaEnv.BaseTypes;

public class Table
{
    public string Fn { get; init; } = "";
    public override string ToString() => Fn;
    protected string Get(char sep = '.', [CallerMemberName] string memberName = "") => string.IsNullOrEmpty(Fn) ? memberName : Fn + sep + memberName;

    protected string GetFunc(string?[] args, char sep = '.', [CallerMemberName] string memberName = "") =>
        Get(sep, memberName) + "(" + string.Join(", ", args.TakeWhile(a => !string.IsNullOrWhiteSpace(a))) + ")";
}
