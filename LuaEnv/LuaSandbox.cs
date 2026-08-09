using System;
using System.Collections.Generic;
using System.Text;
using NLua;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// A Lua interpreter with a restricted environment layered on: <see cref="Env"/> holds a hand-picked subset
/// of the standard library, and every chunk — trusted or not — is loaded against it rather than the real
/// globals. Also exposes the two interpreter primitives <see cref="ModelTranslator"/> needs to materialize a
/// model graph: <see cref="NewTable"/> and <see cref="CompileFunction"/>.
/// </summary>
/// <remarks>
/// Deriving from <see cref="Lua"/> means the unrestricted API (<c>DoString</c>, <c>GetTable</c>, …) is still
/// reachable on this object — the sandbox constrains what *user scripts* can see via <see cref="Env"/>, it is
/// not an enforced boundary for host code.
/// </remarks>
public sealed class LuaSandbox : Lua
{
    #region Sandbox

    private const string BaseEnv =
        """
        return {
          ipairs = ipairs,
          next = next,
          pairs = pairs,
          pcall = pcall,
          tonumber = tonumber,
          tostring = tostring,
          type = type,
          select = select,
          string = { byte = string.byte, char = string.char, find = string.find,
            format = string.format, gmatch = string.gmatch, gsub = string.gsub,
            len = string.len, lower = string.lower, match = string.match,
            rep = string.rep, reverse = string.reverse, sub = string.sub,
            upper = string.upper, pack = string.pack, unpack = string.unpack, packsize = string.packsize },
          table = { concat = table.concat, insert = table.insert, move = table.move, pack = table.pack, remove = table.remove,
            sort = table.sort, unpack = table.unpack },
          math = { abs = math.abs, acos = math.acos, asin = math.asin,
            atan = math.atan, ceil = math.ceil, cos = math.cos,
            deg = math.deg, exp = math.exp, floor = math.floor,
            fmod = math.fmod, huge = math.huge,
            log = math.log, max = math.max, maxinteger = math.maxinteger,
            min = math.min, mininteger = math.mininteger, modf = math.modf, pi = math.pi,
            rad = math.rad, random = math.random, randomseed = math.randomseed, sin = math.sin,
            sqrt = math.sqrt, tan = math.tan, tointeger = math.tointeger, type = math.type, ult = math.ult },
          os = { clock = os.clock, difftime = os.difftime, time = os.time, date = os.date },
          setmetatable = setmetatable,
          getmetatable = getmetatable,
          rawequal = rawequal, rawget = rawget, rawlen = rawlen, rawset = rawset,
          utf8 = { char = utf8.char, charpattern = utf8.charpattern, codepoint = utf8.codepoint, codes = utf8.codes, len = utf8.len, offset = utf8.offset },
          error = error,
        }
        """;

    private const string SandboxFunction =
        """
        return function (untrusted_code, env)
          setmetatable(string, {__index = env.string})
          local untrusted_function, message = load(untrusted_code, nil, 't', env)
          if not untrusted_function then return false, message end
          result = {pcall(untrusted_function)}
          setmetatable(string, nil)
          return table.unpack(result)
        end
        """;

    #endregion

    private readonly LuaFunction _runSandboxed;
    private readonly Dictionary<string, LuaFunction> _compiled = [];

    /// <param name="trustedChunks">
    /// Shipped Lua sources (lualinq, utils) layered into <see cref="Env"/> in order, so their definitions are
    /// in scope for everything loaded afterwards.
    /// </param>
    public LuaSandbox(params string[] trustedChunks)
    {
        State.Encoding = Encoding.UTF8;
        _runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        Env = (LuaTable)DoString(BaseEnv)[0];
        foreach (var chunk in trustedChunks)
            LoadChunk(chunk);
    }

    /// <summary>The restricted environment table every chunk and translated model is loaded against.</summary>
    public LuaTable Env { get; }

    /// <summary>
    /// Runs an untrusted user script against <see cref="Env"/>. Returns the sandbox runner's raw results:
    /// <c>[false, message]</c> when the chunk failed to load or threw, the pcall results otherwise.
    /// </summary>
    public object[] Run(string script) => _runSandboxed.Call(script, Env);

    /// <summary>
    /// Compiles a <c>return function ... end</c> chunk against <see cref="Env"/>. Memoized by source — the
    /// same source in the same env always yields the same function, so the shared <c>getname</c> closure is
    /// minted once no matter how many model nodes carry it.
    /// </summary>
    public LuaFunction CompileFunction(string source) =>
        _compiled.TryGetValue(source, out var fn) ? fn : _compiled[source] = (LuaFunction)_runSandboxed.Call(source, Env)[1];

    /// <summary>
    /// Resolves a value inside <see cref="Env"/> from a path of the form the generated <c>*Names</c> DSL
    /// produces: dot-separated names, each optionally followed by one or more 1-based <c>[n]</c> array
    /// indices — <c>"anime.relations[1].anime.preferredname"</c>.
    /// </summary>
    /// <remarks>
    /// NLua's <see cref="Lua.GetObjectFromPath"/> cannot serve these: it roots at the real globals, which
    /// never hold <see cref="Env"/>, and its splitter only knows <c>'.'</c>.
    /// </remarks>
    /// <returns>The value, or null if any segment is absent or an intermediate is not a table.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is not a value path — an empty segment, malformed brackets, or a callable such
    /// as <c>"anime:getname(Language.English)"</c>.
    /// </exception>
    public object? GetValue(string path)
    {
        object? current = Env;
        foreach (var key in ParseKeys(path))
        {
            if (current is not LuaTable table) return null;
            // The object overload pushes the key verbatim; the string one would re-split it on '.'.
            var next = table[key];
            if (!ReferenceEquals(table, Env)) table.Dispose();
            current = next;
        }

        return current;
    }

    /// <summary>Overload for interior <c>*Names</c> nodes, whose path lives in <c>Fn</c>.</summary>
    public object? GetValue(Names.Table node) => GetValue(node.Fn);

    /// <summary>
    /// Splits a path into the Lua keys to walk: a string per name, an int per <c>[n]</c> index. Validates the
    /// whole path up front, so <see cref="GetValue"/> either walks cleanly or throws before touching Lua.
    /// </summary>
    private static List<object> ParseKeys(string path)
    {
        var keys = new List<object>();
        var i = 0;
        while (true)
        {
            var start = i;
            while (i < path.Length && path[i] is not ('.' or '[')) i++;
            var name = path[start..i];
            if (name.Length == 0)
                throw new ArgumentException($"empty name segment at index {start} in path \"{path}\"", nameof(path));
            // Catches callables: `anime:getname(Language.English)` scans as a name here.
            if (name.AsSpan().IndexOfAny(":()]") >= 0)
                throw new ArgumentException($"\"{name}\" is not a plain name segment in path \"{path}\"", nameof(path));
            keys.Add(name);

            while (i < path.Length && path[i] == '[')
            {
                var close = path.IndexOf(']', i);
                if (close < 0)
                    throw new ArgumentException($"unclosed '[' at index {i} in path \"{path}\"", nameof(path));
                if (!int.TryParse(path.AsSpan(i + 1, close - i - 1), out var index))
                    throw new ArgumentException($"non-numeric index \"{path[(i + 1)..close]}\" in path \"{path}\"", nameof(path));
                keys.Add(index);
                i = close + 1;
            }

            if (i == path.Length) return keys;
            if (path[i] != '.')
                throw new ArgumentException($"expected '.' after an index at index {i} in path \"{path}\"", nameof(path));
            i++;
        }
    }

    /// <summary>
    /// Creates a fresh, empty Lua table. Round-trips through the real globals (which <see cref="Env"/> never
    /// exposes to user scripts) because NLua offers no direct table constructor.
    /// </summary>
    public LuaTable NewTable()
    {
        NewTable("_");
        return GetTable("_");
    }

    /// <summary>
    /// Loads a trusted chunk into <see cref="Env"/>, layering its definitions onto the sandbox globals.
    /// Errors surface as the chunk's own Lua error rather than a return value.
    /// </summary>
    private void LoadChunk(string source) => _runSandboxed.Call(source, Env);
}
