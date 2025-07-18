namespace LuaRenamer.LuaEnv.BaseTypes;

public class ArrayTable<T> : Table where T : Table, new()
{
    public T this[int index] => new() { Fn = Fn + $"[{index}]" };
}
