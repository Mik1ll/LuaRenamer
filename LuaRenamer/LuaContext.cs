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
using Shoko.Abstractions.Video.Enums;
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

    // The shared title-resolver closure for this env build; set by CreateLuaEnv before any producer runs.
    private GetName _getName = null!;


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

    public LuaTable RunSandboxed()
    {
        var runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        var luaEnv = CreateLuaEnv(runSandboxed);
        var retVal = runSandboxed.Call(_args.Configuration.Script, luaEnv);
        if (retVal.Length == 2 && retVal[0] is not true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return luaEnv;
    }

    private LuaTable CreateLuaEnv(LuaFunction runSandboxed)
    {
        var env = (LuaTable)DoString(BaseEnv)[0];
        runSandboxed.Call(_luaLinqText, env);
        runSandboxed.Call(_luaUtilsText, env);
        _getName = GetName.Create(runSandboxed, env, this);

        // Build a plain ILuaModel graph from Shoko data, then materialize it into the env table in one
        // pass. Replaces the old write-through *Table builders; all marshaling lives in LuaSerializer.
        var animes = _args.Series
            .OrderBy(s => s.AnidbAnimeID != _primarySeries.AnidbAnimeID)
            .ThenBy(s => s.AnidbAnimeID)
            .Select(series => ModelProducers.AnimeToModel(series.AnidbAnime, _getName)).ToList();
        var episodes = _args.Episodes
            .OrderBy(e => e.AnidbEpisodeID != _primaryEpisode.AnidbEpisodeID)
            .ThenBy(e => e.AnidbEpisode.SeriesID)
            .ThenBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.AnidbEpisode.Type)
            .ThenBy(e => e.AnidbEpisode.EpisodeNumber)
            .Select(e => ModelProducers.EpisodeToModel(e.AnidbEpisode, _getName, Utils.EpPrefix[e.AnidbEpisode.Type])).ToList();
        var groups = _args.Groups
            .OrderBy(g => g.MainSeriesID != _primarySeries.AnidbAnimeID)
            .Select(g => ModelProducers.GroupToModel(g, _getName)).ToList();

        var model = new EnvModel
        {
            episode_numbers = (EpisodeNumbersDelegate)EpNums,
            logdebug = (LogDelegate)LogDebug,
            log = (LogDelegate)Log,
            logwarn = (LogDelegate)LogWarn,
            logerror = (LogDelegate)LogError,
            replace_illegal_chars = _args.Configuration.ReplaceIllegalCharacters,
            remove_illegal_chars = _args.Configuration.RemoveIllegalCharacters,
            use_existing_anime_location = _args.Configuration.UseExistingAnimeLocation,
            skip_rename = false,
            skip_move = false,
            illegal_chars_map = FilePathCleaner.ReplaceMapDefaults,
            animes = animes,
            anime = animes[0],
            file = ModelProducers.FileToModel(_args.File),
            episodes = episodes,
            episode = episodes[0],
            importfolders = _args.AvailableFolders.Select(ModelProducers.ImportFolderToModel).ToList(),
            groups = groups,
            group = groups.Count > 0 ? groups[0] : null,
            tmdb = ModelProducers.TmdbToModel(
                _args.Series[0].TmdbMovies,
                _args.Series[0].TmdbShows,
                _args.Episodes.Where(e => e.SeriesID == _primarySeries.ID).SelectMany(e => e.TmdbEpisodes),
                _getName),
            ImportFolderType = ModelProducers.EnumTable<DropFolderType>(),
            AnimeType = ModelProducers.EnumTable<AnimeType>(),
            EpisodeType = ModelProducers.EnumTable<EpisodeType>(),
            TitleType = ModelProducers.EnumTable<TitleType>(),
            Language = ModelProducers.EnumTable<TitleLanguage>(),
            RelationType = ModelProducers.EnumTable<RelationType>(),
            SeasonName = ModelProducers.EnumTable<YearlySeason>(),
        };

        new LuaSerializer(GetNewTable).Serialize(model, env);

        return env;
    }

    private LuaTable GetNewTable()
    {
        NewTable("_");
        return GetTable("_");
    }
}
