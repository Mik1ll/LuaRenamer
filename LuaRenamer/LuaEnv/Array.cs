namespace LuaRenamer.LuaEnv;

public class Array<T> : Table where T : Table, new()
{
    public T this[int index] => new() { Fn = Fn + $"[{index}]" };
}
