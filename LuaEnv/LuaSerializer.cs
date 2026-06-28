using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LuaRenamer.LuaEnv.Attributes;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Walks an <see cref="ILuaModel"/> graph once and materializes it into <see cref="LuaTable"/>s.
/// This is the single place every marshaling rule lives — enum→name, null→absent, list→1-based
/// sequence, dictionary→map. It replaces the per-field <c>init =&gt; Set(...)</c> bodies and the
/// <c>ArrayOf</c>/<c>MapOf</c>/<c>EnumToTable</c> overloads scattered across LuaContext.
/// </summary>
/// <remarks>
/// Takes a table factory rather than a <c>Lua</c> instance so it stays decoupled from the host
/// (and trivially unit-testable with a fake factory). No reference-dedup cache here, by design.
/// </remarks>
public sealed class LuaSerializer(Func<LuaTable> newTable)
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
        Delegate or LuaFunction => value,             // free-function delegate / bound Lua callable (incl. GetName), marshaled as-is
        ILuaModel m => WriteModel(m),
        IDictionary dict => WriteMap(dict),           // before IEnumerable (IDictionary : IEnumerable)
        IEnumerable seq => WriteSeq(seq),
        _ => value,                                   // long, double, bool, int, ...
    };

    private LuaTable WriteModel(ILuaModel model)
    {
        var table = newTable();
        foreach (var prop in LuaFields(model.GetType()))
            if (WriteValue(prop.GetValue(model)) is { } v) // null => leave key absent (== Lua nil)
                table[prop.Name] = v;
        return table;
    }

    private LuaTable WriteMap(IDictionary dict)
    {
        var table = newTable();
        foreach (DictionaryEntry entry in dict)
            if (WriteValue(entry.Value) is { } v)
                table[entry.Key is Enum e ? Enum.GetName(e.GetType(), e)! : entry.Key] = v;
        return table;
    }

    private LuaTable WriteSeq(IEnumerable seq)
    {
        var table = newTable();
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
