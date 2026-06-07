using System.Runtime.CompilerServices;
using NLua;

namespace LuaRenamer.LuaEnv.BaseTypes;

public abstract class LuaTableWriter : Table
{
    protected readonly LuaTable _t;

    protected LuaTableWriter(LuaTable t, string? classId = null)
    {
        _t = t;
        if (classId is not null)
            _t["_classid"] = classId;
    }

    protected void Set(object? value, [CallerMemberName] string name = "")
        => _t[name] = value;
}
