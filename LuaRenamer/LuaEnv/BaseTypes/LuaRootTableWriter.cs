using System.Runtime.CompilerServices;
using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

public abstract class LuaRootTableWriter
{
    protected readonly LuaTable _t;

    protected LuaRootTableWriter(LuaTable t)
    {
        _t = t;
    }

    protected void Set(object? value, [CallerMemberName] string name = "")
        => _t[name] = value;
}
