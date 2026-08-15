using System;
using System.Collections.Generic;
using System.Linq;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marker for an enum exposed to user scripts as a Lua table. Carries no data — the contents are fully
/// determined by <typeparamref name="TEnum"/>, so the property need only be declared, never assigned.
/// <see cref="ModelTranslator"/> materializes it as the identity map <c>{ Name = "Name", ... }</c>, which is
/// what lets a script write <c>anime.type == AnimeType.Movie</c> against the name-marshaled enum values.
/// </summary>
/// <typeparam name="TEnum">The CLR enum whose names the table exposes.</typeparam>
public readonly struct LuaEnumTable<TEnum> where TEnum : struct, Enum;

/// <summary>The single definition of "the names this enum exposes", shared by the translator and the generators.</summary>
public static class LuaEnumTable
{
    /// <summary>
    /// The distinct names of <paramref name="enumType"/>'s values, in declaration-value order.
    /// <see cref="Enumerable.Distinct{TSource}(IEnumerable{TSource})"/> collapses aliased values to the one
    /// canonical name <see cref="Enum.GetName(Type, object)"/> returns, so an alias never becomes a second entry.
    /// </summary>
    public static IEnumerable<string> Names(Type enumType) =>
        Enum.GetValues(enumType).Cast<object>().Distinct().Select(v => Enum.GetName(enumType, v)!);

    /// <summary>The <c>TEnum</c> argument of a closed <see cref="LuaEnumTable{TEnum}"/>, else null.</summary>
    public static Type? EnumTypeOf(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(LuaEnumTable<>) ? t.GetGenericArguments()[0] : null;
}
