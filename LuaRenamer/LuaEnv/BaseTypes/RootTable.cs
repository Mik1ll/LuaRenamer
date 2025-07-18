using System.Linq;
using System.Runtime.CompilerServices;

namespace LuaRenamer.LuaEnv.BaseTypes;

public class RootTable
{
    protected static string Get([CallerMemberName] string memberName = "") => memberName;

    protected static string GetFunc(string?[] args, [CallerMemberName] string memberName = "") =>
        Get(memberName) + "(" + string.Join(", ", args.TakeWhile(a => !string.IsNullOrWhiteSpace(a))) + ")";
}
