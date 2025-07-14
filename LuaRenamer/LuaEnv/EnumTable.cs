using System;

namespace LuaRenamer.LuaEnv;

public class EnumTable<T> : Table where T : Enum
{
    public string this[T enumValue] => $"{Fn}.{enumValue}";
}
