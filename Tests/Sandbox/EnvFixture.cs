using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuaRenamer.LuaEnv;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// A hand-described nested value graph, seeded into a sandbox as plain Lua tables. Path resolution is a
/// property of the sandbox alone, so this stands in for a translated env: it exercises the same shapes
/// (nested tables, sequences, sequences of tables, scalar leaves) without a host metadata graph in sight.
/// </summary>
public static class EnvFixture
{
    public abstract record Node;

    public sealed record Leaf(object Value) : Node;

    public sealed record Map(params (string Key, Node Child)[] Entries) : Node;

    public sealed record Seq(params Node[] Items) : Node;

    public static readonly Map Root = new(
        ("alpha", new Map(
            ("name", new Leaf("alpha-name")),
            ("num", new Leaf(7L)),
            ("flag", new Leaf(true)),
            ("inner", new Map(
                ("name", new Leaf("inner-name")),
                ("nums", new Seq(new Leaf(10L), new Leaf(20L), new Leaf(30L))),
                ("items", new Seq(
                    new Map(("name", new Leaf("item-one"))),
                    new Map(("name", new Leaf("item-two"))))))))),
        ("beta", new Map(
            ("grid", new Seq(new Seq(new Map(("name", new Leaf("deep")))))))),
        ("gamma", new Leaf("top-leaf")));

    public static LuaSandbox Seeded()
    {
        var sandbox = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
        foreach ((var key, Node child) in Root.Entries)
        {
            if (sandbox.Run($"{key} = {Render(child)}") is { } error)
                throw new InvalidOperationException($"fixture chunk failed: {error}");
        }

        return sandbox;
    }

    private static string Render(Node node) => node switch
    {
        Leaf { Value: string s } => $"'{s}'",
        Leaf { Value: bool b } => b ? "true" : "false",
        Leaf leaf => Convert.ToString(leaf.Value, CultureInfo.InvariantCulture)!,
        Map map => "{ " + string.Join(", ", map.Entries.Select(e => $"{e.Key} = {Render(e.Child)}")) + " }",
        Seq seq => "{ " + string.Join(", ", seq.Items.Select(Render)) + " }",
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    /// <summary>Every path the fixture makes resolvable, paired with the node it names.</summary>
    public static IReadOnlyList<(string Path, Node Node)> AllPaths { get; } = [.. Walk("", Root)];

    public static IReadOnlyList<string> LeafPaths { get; } = [.. AllPaths.Where(p => p.Node is Leaf).Select(p => p.Path)];

    public static IReadOnlyList<string> MapPaths { get; } = [.. AllPaths.Where(p => p.Node is Map).Select(p => p.Path)];

    public static IReadOnlyList<string> SeqPaths { get; } = [.. AllPaths.Where(p => p.Node is Seq).Select(p => p.Path)];

    private static IEnumerable<(string Path, Node Node)> Walk(string prefix, Node node)
    {
        if (prefix.Length > 0) yield return (prefix, node);
        switch (node)
        {
            case Map map:
                foreach ((var key, Node child) in map.Entries)
                {
                    foreach ((string, Node) found in Walk(prefix.Length == 0 ? key : prefix + "." + key, child))
                        yield return found;
                }

                break;
            case Seq seq:
                for (var i = 0; i < seq.Items.Length; i++)
                {
                    foreach ((string, Node) found in Walk(FormattableString.Invariant($"{prefix}[{i + 1}]"), seq.Items[i]))
                        yield return found;
                }

                break;
            default: // a Leaf has nothing below it
                break;
        }
    }
}
