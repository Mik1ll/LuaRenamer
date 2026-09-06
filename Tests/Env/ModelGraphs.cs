using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FsCheck;
using FsCheck.Fluent;
using LuaRenamer.LuaEnv;

namespace LuaRenamer.Tests.Env;

/// <summary>A generated model graph, labelled so a failing property names the type and the seed that produced it.</summary>
public sealed record ModelGraph(Type Type, ILuaModel Model, int Seed)
{
    public override string ToString() => $"{Type.Name} (seed {Seed})";
}

/// <summary>
/// Builds <see cref="ILuaModel"/> graphs by walking <see cref="LuaSchema.LuaFields"/> — the same description
/// of "which properties are Lua fields" that the runtime serializer and the build-time emitters read. That is
/// what keeps the marshaling properties derived from the schema rather than parallel to it: a field added to a
/// model is generated here without anyone editing this file, and a field whose type this generator cannot
/// handle fails loudly instead of being skipped.
/// </summary>
public static class ModelGraphs
{
    /// <summary>How many levels of nested model a generated graph may have. Bounded from the start: shrinking
    /// through a Lua VM is slow, and an unbounded graph would only make failures harder to read.</summary>
    private const int MaxDepth = 3;

    private const int MaxSequenceLength = 3;

    /// <summary>Every model type in the schema, the root included.</summary>
    public static readonly IReadOnlyList<Type> ModelTypes =
    [
        .. typeof(ILuaModel).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ILuaModel).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The C#-implemented callable contracts. A contract with no entry here throws rather than being skipped,
    /// so adding one to <c>LuaCallables</c> without teaching the suite about it fails in the suite.
    /// </summary>
    private static readonly Dictionary<Type, Func<Delegate>> Callables = new()
    {
        [typeof(EpisodeNumbersDelegate)] = () => new EpisodeNumbersDelegate(pad => "E" + pad.ToString(CultureInfo.InvariantCulture)),
        [typeof(LogDelegate)] = () => new LogDelegate(_ => { }),
    };

    private static readonly NullabilityInfoContext Nullability = new();

    public static Arbitrary<ModelGraph> Graphs() =>
        Gen.Elements<Type>(ModelTypes)
            .SelectMany(type => Gen.Choose(1, 1_000_000).Select(seed => new ModelGraph(type, Build(type, new Random(seed), MaxDepth), seed)))
            .ToArbitrary();

    public static ILuaModel Build(Type type, Random rng, int depth)
    {
        var model = (ILuaModel)Activator.CreateInstance(type)!;
        foreach ((PropertyInfo prop, LuaFieldAttribute _) in LuaSchema.LuaFields(type))
        {
            // A Lua-bodied callable is declared static because its source is the same for every node; the
            // translator reads it off the type, so there is nothing to assign.
            if (prop.GetMethod!.IsStatic) continue;
            prop.SetValue(model, Value(prop.PropertyType, IsNullable(prop), rng, depth));
        }

        return model;
    }

    private static bool IsNullable(PropertyInfo prop) =>
        Nullability.Create(prop).WriteState == NullabilityState.Nullable;

    private static object? Value(Type declared, bool nullable, Random rng, int depth)
    {
        Type type = Nullable.GetUnderlyingType(declared) ?? declared;

        // Never produced: a LuaUnion field is an output the user script writes, and the marker struct has no
        // runtime value to marshal. Production leaves these null and so does this.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LuaUnion<,>)) return null;

        // One in four nullable fields is left empty, which is what exercises null-becomes-absent.
        if (nullable && rng.Next(4) == 0) return null;

        if (type == typeof(string)) return RandomString(rng);
        if (type == typeof(long)) return (long)rng.Next(1, 10_000);
        if (type == typeof(double)) return Math.Round(rng.NextDouble() * 10, 3);
        if (type == typeof(bool)) return rng.Next(2) == 0;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(rng.Next(Enum.GetValues(type).Length));
        if (LuaEnumTable.EnumTypeOf(type) is not null) return Activator.CreateInstance(type);
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return Callables.TryGetValue(type, out Func<Delegate>? make)
                ? make()
                : throw new NotSupportedException($"no generated implementation for callable contract {type.Name}");
        }

        if (typeof(ILuaModel).IsAssignableFrom(type))
        {
            // At the depth bound a nullable model becomes absent and a required one is built without further
            // nesting. That terminates because every model-to-model cycle in the schema runs through a
            // sequence, and sequences are empty at the bound.
            return depth <= 0 ? nullable ? null : Build(type, rng, 0) : Build(type, rng, depth - 1);
        }

        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? Sequence(type.GetGenericArguments()[0], rng, depth)
            : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                ? Map(type.GetGenericArguments(), rng, depth)
                : throw new NotSupportedException($"the model-graph generator has no case for {declared}");
    }

    private static IList Sequence(Type element, Random rng, int depth)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
        if (depth <= 0) return list;
        var length = rng.Next(MaxSequenceLength + 1);
        for (var i = 0; i < length; i++)
        {
            // A null element is deliberate even where the element type is declared non-null: absent elements
            // must compact the sequence rather than leave a hole, and nothing else produces that case.
            var isHole = !element.IsValueType && rng.Next(5) == 0;
            list.Add(isHole ? null : Value(element, nullable: false, rng, depth - 1));
        }

        return list;
    }

    private static IDictionary Map(Type[] args, Random rng, int depth)
    {
        var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
        if (depth <= 0) return map;
        List<object> keys = args[0].IsEnum
            ? [.. Enum.GetValues(args[0]).Cast<object>().Distinct().Take(MaxSequenceLength)]
            : [.. Enumerable.Range(0, rng.Next(1, MaxSequenceLength + 1)).Select(_ => (object)RandomString(rng))];
        foreach (var key in keys)
            map[key] = Value(args[1], nullable: false, rng, depth - 1);

        return map;
    }

    private static string RandomString(Random rng)
    {
        const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string([.. Enumerable.Range(0, rng.Next(1, 9)).Select(_ => Alphabet[rng.Next(Alphabet.Length)])]);
    }
}
