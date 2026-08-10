using System;
using System.Collections;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Walks an <see cref="ILuaModel"/> graph and materializes it into <see cref="LuaTable"/>s inside a
/// <see cref="LuaSandbox"/>. This is the single place every marshaling rule lives — enum→name, null→absent,
/// list→1-based sequence, dictionary→map, <see cref="LuaEnumTable{TEnum}"/>→identity map, and
/// <see cref="LuaFunc{TDelegate}"/>→compiled Lua handle.
/// </summary>
/// <remarks>
/// Lua handles are created here lazily rather than up front, so the model graph stays pure data; the sandbox
/// memoizes identical sources so the shared <c>getname</c> closure is compiled once. No model-reference dedup
/// cache, by design — the graph terminates because nested relation anime are built with
/// <c>includeRelations: false</c>.
/// </remarks>
public sealed class ModelTranslator(LuaSandbox sandbox)
{
    public LuaTable Translate(ILuaModel model) => WriteModel(model, sandbox.NewTable());

    /// <summary>
    /// Writes <paramref name="model"/>'s fields into the existing <paramref name="target"/> table rather
    /// than a fresh one. Used for the env root, whose table is pre-seeded with the sandbox globals and the
    /// lualinq/utils functions before the model fields are layered on top.
    /// </summary>
    public void Translate(ILuaModel model, LuaTable target) => WriteModel(model, target);

    private LuaTable WriteModel(ILuaModel model, LuaTable table)
    {
        // GetValue ignores the instance for a static property, which is how the Lua-bodied callables — the
        // same for every node, so declared static — read back here alongside the per-node data.
        foreach (var (prop, _) in LuaSchema.LuaFields(model.GetType()))
            if (WriteValue(prop.GetValue(model)) is { } v) // null => leave key absent (== Lua nil)
                table[prop.Name] = v;
        return table;
    }

    private object? WriteValue(object? value) => value switch
    {
        null => null,
        string s => s,                               // before IEnumerable (string is IEnumerable<char>)
        Enum e => Enum.GetName(e.GetType(), e),       // the ONE place enums become their name
        Delegate => value,                            // contract implemented in C#, marshaled as-is
        ILuaFunc f => sandbox.CompileFunction(f.Source), // contract implemented in Lua, compiled on demand
        ILuaModel m => WriteModel(m, sandbox.NewTable()),
        IDictionary dict => WriteMap(dict),           // before IEnumerable (IDictionary : IEnumerable)
        IEnumerable seq => WriteSequence(seq),
        // A closed LuaEnumTable<TEnum> is a struct with no members, so it can only be matched by its type.
        _ when LuaEnumTable.EnumTypeOf(value.GetType()) is { } enumType => WriteEnumTable(enumType),
        _ => value,                                   // long, double, bool, int, ...
    };

    /// <summary>The <c>{ Name = "Name", ... }</c> identity map for an exposed enum.</summary>
    private LuaTable WriteEnumTable(Type enumType)
    {
        var table = sandbox.NewTable();
        foreach (var name in LuaEnumTable.Names(enumType))
            table[name] = name;
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
}
