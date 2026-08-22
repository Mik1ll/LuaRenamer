using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using NLua;
using Xunit;

namespace LuaRenamer.Tests.Sandbox;

/// <summary>
/// The exact set of names a user script can reach, pinned. Adding or removing one fails here until this list
/// is updated, which is the point: the sandbox is the only thing standing between a user-authored script and
/// the host process, and a name that arrives unnoticed is how that boundary erodes.
/// </summary>
public sealed class EnvSurfaceTests : IDisposable
{
    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public void Dispose() => _sandbox.Dispose();

    /// <summary>Top-level names: the standard-library subset, then what lualinq and utils.lua add.</summary>
    private static readonly string[] TopLevel =
    [
        "error", "getmetatable", "ipairs", "math", "next", "os", "pairs", "pcall", "rawequal", "rawget",
        "rawlen", "rawset", "select", "setmetatable", "string", "table", "tonumber", "tostring", "type", "utf8",
        "from", "fromArray", "fromArrayInstance", "fromDictionary", "fromIterator", "fromIteratorsArray",
        "fromNothing", "fromSet", "linqSetLogLevel",
    ];

    private static readonly string[] StringNames =
    [
        "byte", "char", "find", "format", "gmatch", "gsub", "len", "lower", "match", "pack", "packsize",
        "rep", "reverse", "sub", "unpack", "upper",
        "cleanspaces", "truncate", // added by utils.lua
    ];

    private static readonly string[] TableNames = ["concat", "insert", "move", "pack", "remove", "sort", "unpack"];

    private static readonly string[] MathNames =
    [
        "abs", "acos", "asin", "atan", "ceil", "cos", "deg", "exp", "floor", "fmod", "huge", "log", "max",
        "maxinteger", "min", "mininteger", "modf", "pi", "rad", "random", "randomseed", "sin", "sqrt", "tan",
        "tointeger", "type", "ult",
    ];

    /// <summary>
    /// Reading the clock is permitted; everything else in <c>os</c> — process control, the filesystem, the
    /// environment, and <c>setlocale</c> — is not.
    /// </summary>
    private static readonly string[] OsNames = ["clock", "date", "difftime", "time"];

    private static readonly string[] Utf8Names = ["char", "charpattern", "codepoint", "codes", "len", "offset"];

    [Fact]
    public void TopLevelNamesAreExactly() => Names(_sandbox.Env).Should().BeEquivalentTo(TopLevel);

    [Theory]
    [InlineData("string")]
    [InlineData("table")]
    [InlineData("math")]
    [InlineData("os")]
    [InlineData("utf8")]
    public void NestedStandardLibrarySubsetsAreExactly(string library) =>
        Names((LuaTable)_sandbox.GetValue(library)!).Should().BeEquivalentTo(Expected(library));

    [Theory]
    // Every one of these outlives the sandbox or reaches past it. setlocale is the sharpest: it sets LC_TIME
    // for the whole host process, so a script could change date rendering for everything until restart.
    [InlineData("os.setlocale")]
    [InlineData("os.getenv")]
    [InlineData("os.execute")]
    [InlineData("os.exit")]
    [InlineData("os.remove")]
    [InlineData("os.rename")]
    [InlineData("os.tmpname")]
    [InlineData("io")]
    [InlineData("require")]
    [InlineData("load")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    [InlineData("debug")]
    [InlineData("package")]
    [InlineData("collectgarbage")]
    public void ProcessGlobalMutatorsAreUnreachable(string path) => _sandbox.GetValue(path).Should().BeNull();

    private static string[] Expected(string library) => library switch
    {
        "string" => StringNames,
        "table" => TableNames,
        "math" => MathNames,
        "os" => OsNames,
        "utf8" => Utf8Names,
        _ => throw new ArgumentOutOfRangeException(nameof(library)),
    };

    private static IEnumerable<string> Names(LuaTable table) => table.Keys.Cast<object>().OfType<string>();
}
