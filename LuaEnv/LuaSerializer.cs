using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LuaRenamer.LuaEnv.Attributes;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Walks an <see cref="ILuaModel"/> graph and materializes it into <see cref="LuaTable"/>s inside a
/// <see cref="LuaSandbox"/>. This is the single place every marshaling rule lives — enum→name, null→absent,
/// list→1-based sequence, dictionary→map, and <see cref="LuaFunctionDef"/>→compiled Lua handle.
/// </summary>
/// <remarks>
/// Lua handles are created here lazily rather than up front, so the model graph stays pure data; the sandbox
/// memoizes identical sources so the shared <c>getname</c> closure is compiled once. No model-reference dedup
/// cache, by design — the graph terminates because nested relation anime are built with
/// <c>includeRelations: false</c>.
/// </remarks>
public sealed class LuaSerializer(LuaSandbox sandbox)
{
    public LuaTable Serialize(ILuaModel model) => WriteModel(model);

    /// <summary>
    /// Writes <paramref name="model"/>'s fields into the existing <paramref name="target"/> table rather
    /// than a fresh one. Used for the env root, whose table is pre-seeded with the sandbox globals and the
    /// lualinq/utils functions before the model fields are layered on top.
    /// </summary>
    public void Serialize(ILuaModel model, LuaTable target)
    {
        foreach (var prop in LuaFields(model.GetType()))
            if (WriteValue(prop.GetValue(model)) is { } v)
                target[prop.Name] = v;
    }

    private object? WriteValue(object? value) => value switch
    {
        null => null,
        string s => s,                               // before IEnumerable (string is IEnumerable<char>)
        Enum e => Enum.GetName(e.GetType(), e),       // the ONE place enums become their name
        Delegate => value,                            // host free-function delegate, marshaled as-is
        LuaFunctionDef def => sandbox.CompileFunction(def.Source), // Lua-bodied callable (getname)
        ILuaModel m => WriteModel(m),
        IDictionary dict => WriteMap(dict),           // before IEnumerable (IDictionary : IEnumerable)
        IEnumerable seq => WriteSequence(seq),
        _ => value,                                   // long, double, bool, int, ...
    };

    private LuaTable WriteModel(ILuaModel model)
    {
        var table = sandbox.NewTable();
        foreach (var prop in LuaFields(model.GetType()))
            if (WriteValue(prop.GetValue(model)) is { } v) // null => leave key absent (== Lua nil)
                table[prop.Name] = v;
        return table;
    }

    private LuaTable WriteMap(IDictionary dict)
    {
        var table = sandbox.NewTable();
        foreach (DictionaryEntry entry in dict)
            if (WriteValue(entry.Value) is { } v)
                table[entry.Key is Enum e ? Enum.GetName(e.GetType(), e)! : entry.Key] = v;
        return table;
    }

    private LuaTable WriteSequence(IEnumerable seq)
    {
        var table = sandbox.NewTable();
        var i = 1;
        foreach (var item in seq)
            if (WriteValue(item) is { } v)
                table[i++] = v;
        return table;
    }

    private static IEnumerable<PropertyInfo> LuaFields(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<LuaFieldAttribute>() is not null);
}
