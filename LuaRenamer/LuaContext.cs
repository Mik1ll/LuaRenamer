using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Relocation;
using File = System.IO.File;

namespace LuaRenamer;

public class LuaContext : Lua
{
    private readonly ILogger _logger;
    private readonly RelocationContext<LuaRenamerSettings> _args;
    private static readonly Stopwatch FileCacheStopwatch = new();
    private static string? _luaUtilsText;
    private static string? _luaLinqText;
    public static readonly string LuaPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua");
    private readonly IShokoSeries _primarySeries;
    private readonly IShokoEpisode _primaryEpisode;

    // The sandbox runner and env table for this build; set by InitSandbox. LuaSerializer reaches back through
    // CompileFunction/NewTable to mint Lua handles and tables on demand.
    private LuaFunction _runSandboxed = null!;
    private LuaTable _env = null!;


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

    #region Lua Function Bindings

    #region Logger Binding

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogDebug(string message) => _logger.LogDebug(message);

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void Log(string message) => _logger.LogInformation(message);

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogWarn(string message) => _logger.LogWarning(message);

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogError(string message) => _logger.LogError(message);

    #endregion

    private string EpNums(long pad) => string.Join(' ', _args.Episodes.Select(se => se.AnidbEpisode)
        .Where(e => e.SeriesID == _primarySeries.AnidbAnimeID)
        .OrderBy(e => e.Type).ThenBy(e => e.EpisodeNumber)
        .Select((e, i) => (e.Type, RangeId: e.EpisodeNumber - i, Num: e.EpisodeNumber)) // RangeId effectively groups sequences of numbers
        .GroupBy(x => (x.Type, x.RangeId))
        .Select(g => g.First().Num is var fn && g.Last().Num is var ln && Utils.EpPrefix[g.Key.Type] is var pre && "D" + pad is var fmt && fn == ln
            ? $"{pre}{fn.ToString(fmt)}"
            : $"{pre}{fn.ToString(fmt)}-{ln.ToString(fmt)}"));

    #endregion

    public LuaContext(ILogger logger, RelocationContext<LuaRenamerSettings> args)
    {
        _logger = logger;
        _args = args;
        _primarySeries = _args.Series.OrderBy(s => s.AnidbAnimeID).First();
        _primaryEpisode = _args.Episodes.Where(e => e.AnidbEpisode.SeriesID == _primarySeries.AnidbAnimeID)
            .OrderBy(e => e.Type == EpisodeType.Other ? int.MinValue : (int)e.Type)
            .ThenBy(e => e.EpisodeNumber)
            .First();
        State.Encoding = Encoding.UTF8;

        if (!FileCacheStopwatch.IsRunning || FileCacheStopwatch.Elapsed > TimeSpan.FromSeconds(10) ||
            string.IsNullOrWhiteSpace(_luaUtilsText) ||
            string.IsNullOrWhiteSpace(_luaLinqText))
        {
            _luaUtilsText = File.ReadAllText(Path.Combine(LuaPath, "utils.lua"));
            _luaLinqText = File.ReadAllText(Path.Combine(LuaPath, "lualinq.lua"));
        }

        FileCacheStopwatch.Restart();
    }

    /// <summary>
    /// Test-only ctor: stands up just the sandbox env (lualinq/utils loaded) so a <see cref="LuaSerializer"/>
    /// can be exercised against a real interpreter without the full Shoko relocation context.
    /// </summary>
    internal LuaContext()
    {
        _logger = null!;
        _args = null!;
        _primarySeries = null!;
        _primaryEpisode = null!;
        State.Encoding = Encoding.UTF8;
        _luaUtilsText = File.ReadAllText(Path.Combine(LuaPath, "utils.lua"));
        _luaLinqText = File.ReadAllText(Path.Combine(LuaPath, "lualinq.lua"));
        InitSandbox();
    }

    public LuaTable RunSandboxed()
    {
        InitSandbox();
        PopulateEnv();
        var retVal = _runSandboxed.Call(_args.Configuration.Script, _env);
        if (retVal.Length == 2 && retVal[0] is not true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return _env;
    }

    // Creates the sandbox runner and the env table, then layers lualinq + utils into the env.
    private void InitSandbox()
    {
        _runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        _env = (LuaTable)DoString(BaseEnv)[0];
        _runSandboxed.Call(_luaLinqText, _env);
        _runSandboxed.Call(_luaUtilsText, _env);
    }

    /// <summary>Creates a fresh, empty Lua table (used by <see cref="LuaSerializer"/>).</summary>
    public LuaTable NewTable()
    {
        NewTable("_");
        return GetTable("_");
    }

    /// <summary>Compiles a <c>return function ... end</c> chunk against the sandbox env (used by <see cref="LuaSerializer"/>).</summary>
    public LuaFunction CompileFunction(string source) => (LuaFunction)_runSandboxed.Call(source, _env)[1];

    private void PopulateEnv()
    {
        // ModelProducers builds the whole ILuaModel graph from the relocation args; we supply the host policy
        // it can't derive (illegal-char config, default replacement map, host-bound delegates). LuaSerializer
        // then materializes it into the env table in one pass — all marshaling lives there.
        var model = ModelProducers.EnvToModel(
            _args,
            _primarySeries,
            _primaryEpisode,
            Utils.EpPrefix,
            _args.Configuration.ReplaceIllegalCharacters,
            _args.Configuration.RemoveIllegalCharacters,
            _args.Configuration.UseExistingAnimeLocation,
            FilePathCleaner.ReplaceMapDefaults,
            EpNums,
            LogDebug,
            Log,
            LogWarn,
            LogError);

        new LuaSerializer(this).Serialize(model, _env);
    }
}
