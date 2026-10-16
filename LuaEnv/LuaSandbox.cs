using System;
using System.Collections.Generic;
using System.Text;
using KeraLua;
using NLua;
using NLua.Exceptions;
using Lua = NLua.Lua;
using LuaFunction = NLua.LuaFunction;

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
        -- Finalizers run with debug hooks off, and at lua_close outside the memory bound, so a script's __gc
        -- would escape both limits. Lua only registers a __gc that is in the metatable when it is set, and
        -- debug.setmetatable is unpublished, so refusing it here closes that route.
        local function sandboxed_setmetatable(t, mt)
          if type(mt) == 'table' and rawget(mt, '__gc') ~= nil then
            error('__gc finalizers are not available to scripts', 2)
          end
          return setmetatable(t, mt)
        end

        local env = {
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
          setmetatable = sandboxed_setmetatable,
          getmetatable = getmetatable,
          rawequal = rawequal, rawget = rawget, rawlen = rawlen, rawset = rawset,
          utf8 = { char = utf8.char, charpattern = utf8.charpattern, codepoint = utf8.codepoint, codes = utf8.codes, len = utf8.len, offset = utf8.offset },
          error = error,
        }

        -- A method call on a string value resolves through the string metatable's __index, which otherwise
        -- reaches the whole real library (string.dump included). Pointing it at env.string makes the published
        -- subset the only route; env.string is never rebound, so helpers utils.lua defines later land too.
        getmetatable('').__index = env.string
        return env
        """;

    // Loads a chunk against `env` and pcalls it. Returns [false, error] if the chunk failed to load or threw,
    // otherwise pcall's results. `tostring` on the error keeps a non-string error object (`error({})`) reportable.
    //
    // With a `budget`, a count hook bounds the run. It is Lua, not a managed hook, because raising from managed
    // code would longjmp across .NET frames. Once spent it fires on every instruction outside this function, so
    // a script that pcalls the error raises again at its next instruction.
    // The hook is per coroutine, so it only bounds the script while `coroutine` stays out of env.
    // `tostring` stays under the hook because a `__tostring` metamethod is script code.
    //
    // The memory bound is switched on from in here, not before the call, because NLua pushes the arguments
    // outside any pcall, where a refused allocation would panic the state. The same goes for the results,
    // which is why a bounded run returns no value of the script's. A refusal stops the script the way a spent
    // budget does, since retrying under pcall would cost an emergency full collection per attempt.
    private const string SandboxFunction =
        """
        local enforce, refused = enforce_memory_bound, memory_refused
        enforce_memory_bound, memory_refused = nil, nil
        local run
        run = function (untrusted_code, env, budget)
          if budget then enforce() end
          local untrusted_function, message = load(untrusted_code, nil, 't', env)
          if not untrusted_function then return false, message end
          if not budget then
            local result = {pcall(untrusted_function)}
            if not result[1] then return false, tostring(result[2]) end
            return table.unpack(result)
          end

          local remaining, stop = budget, nil
          local function hook()
            if not stop then
              remaining = remaining - 1000
              if remaining > 0 and not refused() then return end
              stop = refused() and 'memory bound exceeded' or string.format('instruction budget of %d exceeded', budget)
              debug.sethook(hook, '', 1)
            end
            if debug.getinfo(2, 'f').func ~= run then error(stop, 2) end
          end

          debug.sethook(hook, '', 1000)
          local ok, err = pcall(untrusted_function)
          if not ok then
            local converted, text = pcall(tostring, err)
            err = (converted or type(text) == 'string') and text or 'error object could not be converted to a string'
          end
          debug.sethook()
          if ok then return true end
          return false, err
        end
        return run
        """;

    private const string TableConstructor = "return function () return {} end";

    #endregion

    private readonly LuaFunction _runSandboxed;
    private readonly LuaFunction _newTable;
    private readonly Dictionary<string, LuaFunction> _compiled = [];
    private readonly ScriptLimits _limits;

    // Native code holds only function pointers to these, which don't keep the delegates alive.
    private readonly LuaAlloc _allocate;
    private readonly KeraLua.LuaFunction _enforceMemoryBound;
    private readonly KeraLua.LuaFunction _memoryRefused;

    private readonly LuaAlloc _nativeAllocate;
    private bool _enforcing;
    private bool _refused;

    /// <inheritdoc cref="LuaSandbox(ScriptLimits, string[])"/>
    public LuaSandbox(params string[] trustedChunks) : this(ScriptLimits.Default, trustedChunks)
    {
    }

    /// <param name="limits">What each <see cref="Run"/> may use. Trusted chunks and translation are not limited.</param>
    /// <param name="trustedChunks">
    /// Shipped Lua sources (lualinq, utils) layered into <see cref="Env"/> in order, so their definitions are
    /// in scope for everything loaded afterwards.
    /// </param>
    public LuaSandbox(ScriptLimits limits, params string[] trustedChunks)
    {
        _limits = limits;
        State.Encoding = Encoding.UTF8;

        // Wrapped rather than replaced: the state's existing blocks came from the native library's heap, and
        // only its own allocator may free them.
        var userData = IntPtr.Zero;
        _nativeAllocate = State.GetAllocFunction(ref userData);
        AllocatedBytes = (State.GarbageCollector(LuaGC.Count, 0) * 1024L) + State.GarbageCollector(LuaGC.Countb, 0);
        _allocate = Allocate;
        State.SetAllocFunction(_allocate, ref userData);
        _enforceMemoryBound = _ =>
        {
            _enforcing = true;
            return 0;
        };
        State.Register("enforce_memory_bound", _enforceMemoryBound);
        _memoryRefused = luaState =>
        {
            KeraLua.Lua.FromIntPtr(luaState).PushBoolean(_refused);
            return 1;
        };
        State.Register("memory_refused", _memoryRefused);

        _runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        _newTable = (LuaFunction)DoString(TableConstructor)[0];
        Env = (LuaTable)DoString(BaseEnv)[0];
        foreach (var chunk in trustedChunks)
            LoadChunk(chunk);
    }

    /// <summary>
    /// The most bytes the state has held at once since the latest <see cref="Run"/> began, or since
    /// construction before the first.
    /// </summary>
    public long PeakAllocatedBytes { get; private set; }

    /// <summary>The bytes the state holds now, as its allocator has counted them.</summary>
    public long AllocatedBytes { get; private set; }

    /// <summary>The restricted environment table every chunk and translated model is loaded against.</summary>
    public LuaTable Env { get; }

    /// <summary>
    /// Runs an untrusted user script against <see cref="Env"/>, within the sandbox's <see cref="ScriptLimits"/>.
    /// </summary>
    /// <returns>
    /// <c>null</c> if the script loaded and ran to completion, otherwise the load or runtime error message.
    /// Exceeding a limit is an error, and its message names the limit.
    /// </returns>
    public string? Run(string script)
    {
        _refused = false;
        PeakAllocatedBytes = AllocatedBytes;
        string? error;
        try
        {
            error = ErrorOf(_runSandboxed.Call(script, Env, _limits.Instructions));
        }
        catch (LuaException) when (_refused)
        {
            // Refused outside the script's own pcall, which can be after the hook was installed.
            State.SetHook(null, LuaHookMask.Disabled, 0);
            return MemoryBoundExceeded;
        }
        finally
        {
            // Stryker disable once Boolean : left on, the next host-side push outside any pcall panics the state, which no test can assert on
            _enforcing = false;
        }

        return _refused && error is not null ? MemoryBoundExceeded : error;
    }

    private string MemoryBoundExceeded => $"memory bound of {_limits.MemoryBytes} bytes exceeded";

    /// <summary>
    /// Every allocation, resize and free of the state passes through here. Growth past the bound is refused
    /// while a script runs; shrinks and frees are always passed on, because Lua requires them to succeed.
    /// </summary>
    private IntPtr Allocate(IntPtr userData, IntPtr block, nuint oldSize, nuint newSize)
    {
        // Without a block, oldSize is the type of object being created, not a size.
        var growth = (long)newSize - (long)(block == IntPtr.Zero ? 0 : oldSize);
        // The room is floored at zero so that a shrink is never refused, even with the state already over the bound.
        if (_enforcing && growth > Math.Max(0, _limits.MemoryBytes - AllocatedBytes))
        {
            _refused = true;
            // Brings the hook in at the script's next instruction instead of up to a thousand later.
            // lua_sethook is safe to call from anywhere, this allocator included.
            if (State.Hook is { } hook)
                State.SetHook(hook, State.HookMask, 1);
            return IntPtr.Zero;
        }

        var result = _nativeAllocate(userData, block, oldSize, newSize);
        if (result != IntPtr.Zero || newSize == 0)
        {
            AllocatedBytes += growth;
            PeakAllocatedBytes = Math.Max(PeakAllocatedBytes, AllocatedBytes);
        }

        return result;
    }

    // The run function's error is load's message or tostring's result, so it is always a string.
    private static string? ErrorOf(object[] result) => result is [not true, var error, ..] ? (string)error : null;

    /// <summary>
    /// Compiles a <c>return function ... end</c> chunk against <see cref="Env"/>. Memoized by source — the
    /// same source in the same env always yields the same function, so the shared <c>getname</c> closure is
    /// minted once no matter how many model nodes carry it.
    /// </summary>
    public LuaFunction CompileFunction(string source)
    {
        return _compiled.TryGetValue(source, out LuaFunction? cached)
            ? cached
            : (_compiled[source] = _runSandboxed.Call(source, Env) switch
            {
                [true, LuaFunction fn, ..] => fn,
                [not true, var error, ..] => throw new ArgumentException($"chunk did not compile: {error}", nameof(source)),
                _ => throw new ArgumentException("chunk did not return a function", nameof(source)),
            });
    }

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

    /// <summary>Overload for interior <c>*Names</c> nodes, whose path lives in <c>Path</c>.</summary>
    public object? GetValue(Names.NamesNode node) => GetValue(node.Path);

    /// <summary>
    /// Splits a path into the Lua keys to walk: a string per name, an int per <c>[n]</c> index. Validates the
    /// whole path up front, so <see cref="GetValue(string)"/> either walks cleanly or throws before touching Lua.
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
                if (close == -1)
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
    /// Creates a fresh, empty Lua table. Goes through a compiled constructor rather than
    /// <see cref="Lua.NewTable(string)"/>, which would have to name the table in the real globals to hand it back.
    /// </summary>
    public LuaTable NewTable() => (LuaTable)_newTable.Call()[0];

    /// <summary>
    /// Loads a trusted chunk into <see cref="Env"/>, layering its definitions onto the sandbox globals.
    /// </summary>
    private void LoadChunk(string source)
    {
        if (ErrorOf(_runSandboxed.Call(source, Env)) is { } error)
            throw new ArgumentException($"trusted chunk failed to load: {error}", nameof(source));
    }
}
