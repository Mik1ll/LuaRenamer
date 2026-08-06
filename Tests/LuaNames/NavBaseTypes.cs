using System.Linq;
using System.Runtime.CompilerServices;

namespace LuaRenamer.LuaEnv.Names;

// Hand-written base types for the generated *Names navigation DSL (see DefsGenerator/ModelNamesGenerator.cs,
// which emits one companion class per ILuaModel record into obj/ at build time). A Names instance carries the
// Lua path built so far in Fn and renders it via ToString, so `EnvNames.anime.relations[1].type` interpolates
// to "anime.relations[1].type" and stops compiling when the schema moves.

public class Table
{
    public string Fn { get; init; } = "";
    public override string ToString() => Fn;
    protected string Get(char sep = '.', [CallerMemberName] string memberName = "") => string.IsNullOrEmpty(Fn) ? memberName : Fn + sep + memberName;

    /// <summary>
    /// Renders a call. Trailing omitted optional arguments arrive as null and are dropped, so both
    /// <c>getname(lang)</c> and <c>getname(lang, unofficial)</c> render correctly.
    /// </summary>
    protected string GetFunc(string?[] args, char sep = '.', [CallerMemberName] string memberName = "") =>
        Get(sep, memberName) + "(" + string.Join(", ", args.TakeWhile(a => !string.IsNullOrWhiteSpace(a))) + ")";
}

public class ArrayTable<T> : Table where T : Table, new()
{
    public T this[int index] => new() { Fn = Fn + $"[{index}]" };
}

public class EnumTable<T> : Table where T : System.Enum
{
    public string this[T enumValue] => $"{Fn}.{enumValue}";
}
