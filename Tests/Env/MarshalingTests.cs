using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using FsCheck.Xunit;
using LuaRenamer.LuaEnv;
using NLua;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The rules by which a model graph becomes a script-visible environment, verified over generated graphs
/// rather than enumerated examples. Every property here reads the schema through <see cref="LuaSchema"/>, the
/// same walk <see cref="ModelTranslator"/> and the defs emitters use — so a schema change that lands in only
/// one of them fails here rather than in CI's diff on the generated defs.
/// </summary>
public sealed class MarshalingTests : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    private ModelTranslator Translator => new(_sandbox);

    public void Dispose() => _sandbox.Dispose();

    [Property(Arbitrary = [typeof(ModelGraphs)])]
    public void EveryGeneratedGraphMarshalsAndSatisfiesTheRules(ModelGraph graph)
    {
        // Termination is asserted by arriving here at all: an unbounded recursion would not return.
        LuaTable table = Translator.Translate(graph.Model);
        AssertModel(graph.Model, table);
    }

    [Property(Arbitrary = [typeof(ModelGraphs)])]
    public void MaterializingTheSameGraphTwiceIsStructurallyEqual(ModelGraph graph)
    {
        ModelTranslator translator = Translator;
        Structure(translator.Translate(graph.Model)).Should().BeEquivalentTo(Structure(translator.Translate(graph.Model)));
    }

    [Fact]
    public void SchemaConformanceRejectsAnUndeclaredKey()
    {
        // The guard the generated properties rely on, checked against a table that really does carry a key no
        // model declares — otherwise a conformance check that never fails would look like passing coverage.
        LuaTable table = Translator.Translate(ModelGraphs.Build(typeof(LuaEnv.Models.AnimeModel), new Random(1), 2));
        table["notafield"] = "smuggled";

        FluentActions.Invoking(() => AssertSchemaConformance(typeof(LuaEnv.Models.AnimeModel), table))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void TheGeneratorCoversEveryModelInTheSchema() =>
        ModelGraphs.ModelTypes.Should().HaveCountGreaterThan(15)
            .And.Contain(typeof(LuaEnv.Models.EnvModel))
            .And.Contain(typeof(LuaEnv.Models.AnimeModel));

    #region Rules

    private void AssertModel(ILuaModel model, LuaTable table)
    {
        AssertSchemaConformance(model.GetType(), table);

        foreach ((PropertyInfo prop, LuaFieldAttribute _) in LuaSchema.LuaFields(model.GetType()))
        {
            var value = prop.GetValue(model);
            var marshaled = table[prop.Name];

            if (value is null)
            {
                marshaled.Should().BeNull($"{prop.Name} holds no value, so its key must be absent rather than present-and-empty");
                continue;
            }

            AssertValue(value, marshaled.Should().NotBeNull($"{prop.Name} holds a value").And.Subject, prop.Name);
        }
    }

    private void AssertValue(object value, object marshaled, string where)
    {
        switch (value)
        {
            case string s:
                marshaled.Should().Be(s, where);
                break;
            case Enum e:
                // The one place enums become their name, and the name must parse back to the same member.
                marshaled.Should().Be(Enum.GetName(e.GetType(), e), where);
                Enum.Parse(e.GetType(), (string)marshaled).Should().Be(e, where);
                break;
            case ILuaModel nested:
                AssertModel(nested, marshaled.Should().BeOfType<LuaTable>(where).Subject);
                break;
            case IDictionary map:
                AssertMap(map, marshaled.Should().BeOfType<LuaTable>(where).Subject, where);
                break;
            case IEnumerable sequence:
                AssertSequence(sequence, marshaled.Should().BeOfType<LuaTable>(where).Subject, where);
                break;
            case Delegate:
                AssertCallable(marshaled, where);
                break;
            default:
                AssertLeaf(marshaled, where);
                break;
        }
    }

    private void AssertSequence(IEnumerable sequence, LuaTable table, string where)
    {
        var present = sequence.Cast<object?>().Where(item => item is not null).ToList();

        // Keyed from 1 with no gaps: absent elements compact the sequence rather than leaving holes, which is
        // what makes `#seq` and ipairs agree with what the producer actually had.
        for (var i = 0; i < present.Count; i++)
            AssertValue(present[i]!, table[i + 1].Should().NotBeNull($"{where}[{i + 1}]").And.Subject, $"{where}[{i + 1}]");
        table[present.Count + 1].Should().BeNull($"{where} has exactly {present.Count} elements");
        table.Keys.Cast<object>().Should().HaveCount(present.Count, where);
    }

    private void AssertMap(IDictionary map, LuaTable table, string where)
    {
        foreach (DictionaryEntry entry in map)
        {
            var key = entry.Key is Enum e ? Enum.GetName(e.GetType(), e) ?? "" : entry.Key;
            if (entry.Value is null)
                table[key].Should().BeNull($"{where}[{key}]");
            else
                AssertValue(entry.Value, table[key].Should().NotBeNull($"{where}[{key}]").And.Subject, $"{where}[{key}]");
        }
    }

    /// <summary>
    /// A callable implemented in C# arrives as callable userdata rather than a Lua function — that is how NLua
    /// marshals a CLR delegate. What matters is that a script can call it, so that is what is asserted.
    /// </summary>
    private void AssertCallable(object marshaled, string where)
    {
        _sandbox.Env["probe"] = marshaled;
        _sandbox.Run("probe_type = type(probe)").Should().BeNull(where);
        _sandbox.GetValue("probe_type").Should().BeOneOf(["function", "userdata"], where);
    }

    private static void AssertLeaf(object marshaled, string where) =>
        marshaled.Should().Match(v =>
            v is string || v is long || v is double || v is bool || v is LuaTable || v is LuaFunction, where);

    /// <summary>Every key present on a materialized model is a declared field of that model type.</summary>
    private static void AssertSchemaConformance(Type modelType, LuaTable table)
    {
        var declared = LuaSchema.LuaFields(modelType).Select(f => f.Prop.Name).ToHashSet(StringComparer.Ordinal);
        table.Keys.Cast<object>().OfType<string>().Should().OnlyContain(key => declared.Contains(key),
            $"{modelType.Name} declares {declared.Count} Lua fields and the materialized table must carry no others");
    }

    /// <summary>A comparable snapshot of a table's shape and values, for the repeatability property.</summary>
    private static List<object?> Structure(LuaTable table) =>
    [
        .. table.Keys.Cast<object>().OrderBy(k => k.ToString(), StringComparer.Ordinal).Select(object? (key) => new
        {
            Key = key.ToString(),
            Value = table[key] switch
            {
                LuaTable nested => Structure(nested),
                LuaFunction => "<function>",
                var other => other?.GetType().IsClass == true && other is not string ? other.GetType().Name : other,
            },
        }),
    ];

    #endregion
}
