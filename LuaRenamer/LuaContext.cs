using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;
using File = System.IO.File;

namespace LuaRenamer;

public class LuaContext : Lua
{
    private readonly ILogger _logger;
    private readonly RelocationEventArgs<LuaRenamerSettings> _args;
    private static readonly Stopwatch FileCacheStopwatch = new();
    private static string? _luaUtilsText;
    private static string? _luaLinqText;
    public static readonly string LuaPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua");
    private readonly Dictionary<(Type, int), Dictionary<string, object?>> _tableCache = new();


    #region Sandbox

    private const string BaseEnv = @"
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
";

    private const string SandboxFunction = @"
return function (untrusted_code, env)
  setmetatable(string, {__index = env.string})
  local untrusted_function, message = load(untrusted_code, nil, 't', env)
  if not untrusted_function then return nil, message end
  result = {pcall(untrusted_function)}
  setmetatable(string, nil)
  return table.unpack(result)
end
";

    #endregion

    #region Lua Function Bindings

    #region Logger Binding

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogDebug(string message) => _logger.LogDebug(message);
    private static readonly MethodInfo LogDebugMethod = typeof(LuaContext).GetMethod(nameof(LogDebug), BindingFlags.Instance | BindingFlags.NonPublic)!;

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void Log(string message) => _logger.LogInformation(message);
    private static readonly MethodInfo LogMethod = typeof(LuaContext).GetMethod(nameof(Log), BindingFlags.Instance | BindingFlags.NonPublic)!;

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogWarn(string message) => _logger.LogWarning(message);
    private static readonly MethodInfo LogWarnMethod = typeof(LuaContext).GetMethod(nameof(LogWarn), BindingFlags.Instance | BindingFlags.NonPublic)!;

    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    private void LogError(string message) => _logger.LogError(message);
    private static readonly MethodInfo LogErrorMethod = typeof(LuaContext).GetMethod(nameof(LogError), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    private static readonly string GetNameFunction =
        $$"""
          ---@param self Anime|Episode
          ---@param lang Language
          ---@param include_unofficial? boolean
          ---@return string?
          return function (self, lang, include_unofficial)
              local title_priority = {
                  {{nameof(TitleType.Main)}} = 0,
                  {{nameof(TitleType.Official)}} = 1,
                  {{nameof(TitleType.None)}} = 2,
                  {{nameof(TitleType.Synonym)}} = include_unofficial and 3 or nil,
              }
              ---@type string?
              local name = from(self.{{nameof(Anime.titles)}}):where(function(t1) ---@param t1 Title
                  return t1.{{nameof(Title.language)}} == lang and title_priority[t1.{{nameof(Title.type)}}] ~= nil
              end):orderby(function(t2) ---@param t2 Title
                  return title_priority[t2.{{nameof(Title.type)}}]
              end):select("{{nameof(Title.name)}}"):first()
              return name
          end
          """;

    private string EpNums(int pad) => _args.Episodes.Select(se => se.AnidbEpisode)
        .Where(e => e.SeriesID == _args.Series.First().AnidbAnimeID) // All episodes with same anime id
        .OrderBy(e => e.EpisodeNumber)
        .GroupBy(e => e.Type)
        .OrderBy(g => g.Key)
        .Aggregate("", (epString, epTypeGroup) =>
            epString + " " + epTypeGroup.Aggregate( // Combine substring for each episode type
                (InRange: false, LastNum: -1, EpSubstring: ""),
                (tuple, ep) => (ep.EpisodeNumber == tuple.LastNum + 1, ep.EpisodeNumber,
                        tuple.EpSubstring +
                        (ep.EpisodeNumber != tuple.LastNum + 1 // Append to string if not in a range
                            ? (tuple.InRange ? "-" + tuple.LastNum.ToString($"d{pad}") : "") + // If a range ended, append the last number
                              " " + Utils.EpPrefix[epTypeGroup.Key] + ep.EpisodeNumber.ToString($"d{pad}") // Add current prefix and number
                            : "")
                    ),
                tuple => tuple.EpSubstring + (tuple.InRange ? "-" + tuple.LastNum.ToString($"d{pad}") : "") // If a range ended, append the last number
            ).Trim()
        ).Trim();

    private static readonly MethodInfo EpNumsMethod =
        typeof(LuaContext).GetMethod(nameof(EpNums), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    public LuaContext(ILogger logger, RelocationEventArgs<LuaRenamerSettings> args)
    {
        _logger = logger;
        _args = args;
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

    public Dictionary<object, object> RunSandboxed()
    {
        var runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        var luaEnv = (LuaTable)DoString(BaseEnv)[0];
        var env = CreateLuaEnv(luaEnv, runSandboxed);
        foreach (var (k, v) in env) this.AddObject(luaEnv, v, k);
        var retVal = runSandboxed.Call(_args.Settings.Script, luaEnv);
        if (retVal.Length == 2 && (bool)retVal[0] != true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return GetTableDict(luaEnv);
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> CreateLuaEnv(LuaTable luaEnv, LuaFunction runSandboxed)
    {
        luaEnv[nameof(Env.logdebug)] = RegisterFunction("_", this, LogDebugMethod);
        luaEnv[nameof(Env.log)] = RegisterFunction("_", this, LogMethod);
        luaEnv[nameof(Env.logwarn)] = RegisterFunction("_", this, LogWarnMethod);
        luaEnv[nameof(Env.logerror)] = RegisterFunction("_", this, LogErrorMethod);
        luaEnv[nameof(Env.episode_numbers)] = RegisterFunction("_", this, EpNumsMethod);
        runSandboxed.Call(_luaLinqText, luaEnv);
        runSandboxed.Call(_luaUtilsText, luaEnv);
        var getNameFn = (LuaFunction)runSandboxed.Call(GetNameFunction, luaEnv)[1];

        var animes = _args.Series.OrderBy(s => s.AnidbAnimeID).Select(series => AnimeToDict(series.AnidbAnime, false, getNameFn)).ToList();
        var episodes = _args.Episodes.Select(e => e.AnidbEpisode)
            .OrderBy(e => e.Type == EpisodeType.Other ? int.MinValue : (int)e.Type)
            .ThenBy(e => e.EpisodeNumber)
            .Select(e => EpisodeToDict(e, getNameFn)).ToList();
        var groups = _args.Groups.Select(g => GroupToDict(g, getNameFn));

        var env = new Dictionary<string, object?>();
        env.Add(nameof(Env.filename), null);
        env.Add(nameof(Env.destination), null);
        env.Add(nameof(Env.subfolder), null);
        env.Add(nameof(Env.replace_illegal_chars), false);
        env.Add(nameof(Env.remove_illegal_chars), false);
        env.Add(nameof(Env.use_existing_anime_location), false);
        env.Add(nameof(Env.skip_rename), false);
        env.Add(nameof(Env.skip_move), false);
        env.Add(nameof(Env.animes), animes);
        env.Add(nameof(Env.anime), animes.First());
        env.Add(nameof(Env.file), FileToDict(_args.File));
        env.Add(nameof(Env.episodes), episodes);
        env.Add(nameof(Env.episode), episodes.First());
        env.Add(nameof(Env.importfolders), _args.AvailableFolders.Select(ImportFolderToDict).ToList());
        env.Add(nameof(Env.groups), groups);
        env.Add(nameof(Env.group), groups.FirstOrDefault());
        env.Add(nameof(Env.AnimeType), EnumToDict<AnimeType>());
        env.Add(nameof(Env.TitleType), EnumToDict<TitleType>());
        env.Add(nameof(Env.Language), EnumToDict<TitleLanguage>());
        env.Add(nameof(Env.EpisodeType), EnumToDict<EpisodeType>());
        env.Add(nameof(Env.ImportFolderType), EnumToDict<DropFolderType>());
        env.Add(nameof(Env.RelationType), EnumToDict<RelationType>());
        return env;
    }

    private Dictionary<string, string> EnumToDict<T>() => Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a!.ToString()!, a => a!.ToString()!);

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> GroupToDict(IShokoGroup g, LuaFunction getNameFn)
    {
        var groupdict = new Dictionary<string, object?>();
        groupdict.Add(nameof(Group.name), g.PreferredTitle);
        groupdict.Add(nameof(Group.mainanime), AnimeToDict(g.MainSeries.AnidbAnime, false, getNameFn));
        groupdict.Add(nameof(Group.animes), g.AllSeries.Select(a => AnimeToDict(a.AnidbAnime, false, getNameFn)).ToList());
        return groupdict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> AnimeToDict(ISeries anime, bool ignoreRelations, LuaFunction getNameFn)
    {
        if (anime == null) throw new ArgumentNullException(nameof(anime));
        if (_tableCache.TryGetValue((typeof(ISeries), anime.ID), out var eObj))
            return eObj;
        var series = anime.ShokoSeries.FirstOrDefault();
        var animedict = new Dictionary<string, object?>();
        animedict.Add(nameof(Anime.airdate), anime.AirDate?.ToTable());
        animedict.Add(nameof(Anime.enddate), anime.EndDate?.ToTable());
        animedict.Add(nameof(Anime.rating), anime.Rating);
        animedict.Add(nameof(Anime.restricted), anime.Restricted);
        animedict.Add(nameof(Anime.type), anime.Type.ToString());
        animedict.Add(nameof(Anime.preferredname), string.IsNullOrWhiteSpace(series?.PreferredTitle) ? anime.PreferredTitle : series.PreferredTitle);
        animedict.Add(nameof(Anime.defaultname), string.IsNullOrWhiteSpace(series?.DefaultTitle) ? anime.DefaultTitle : series.DefaultTitle);
        animedict.Add(nameof(Anime.id), anime.ID);
        animedict.Add(nameof(Anime.titles), anime.Titles.Select(TitleToDict));
        animedict.Add(nameof(Anime.getname), getNameFn);
        animedict.Add(nameof(Anime._classid), Anime._classidVal);
        var epcountdict = new Dictionary<string, int>();
        epcountdict.Add(EpisodeType.Episode.ToString(), anime.EpisodeCounts.Episodes);
        epcountdict.Add(EpisodeType.Special.ToString(), anime.EpisodeCounts.Specials);
        epcountdict.Add(EpisodeType.Credits.ToString(), anime.EpisodeCounts.Credits);
        epcountdict.Add(EpisodeType.Trailer.ToString(), anime.EpisodeCounts.Trailers);
        epcountdict.Add(EpisodeType.Other.ToString(), anime.EpisodeCounts.Others);
        epcountdict.Add(EpisodeType.Parody.ToString(), anime.EpisodeCounts.Parodies);
        animedict.Add(nameof(Anime.episodecounts), epcountdict);
        animedict.Add(nameof(Anime.relations), ignoreRelations
            ? []
            : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                .Select(r =>
                {
                    var relationdict = new Dictionary<string, object?>();
                    relationdict.Add(nameof(Relation.type), r.RelationType.ToString());
                    relationdict.Add(nameof(Relation.anime), AnimeToDict(r.Related!, true, getNameFn));
                    return relationdict;
                }).ToList());
        return _tableCache[(typeof(ISeries), anime.ID)] = animedict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? AniDbFileToDict(IAniDBFile? aniDbFile)
    {
        if (aniDbFile is null)
            return null;
        var anidb = new Dictionary<string, object?>();
        anidb.Add(nameof(AniDb.id), aniDbFile.AniDBFileID);
        anidb.Add(nameof(AniDb.censored), aniDbFile.Censored);
        anidb.Add(nameof(AniDb.source), aniDbFile.Source);
        anidb.Add(nameof(AniDb.version), aniDbFile.Version);
        anidb.Add(nameof(AniDb.releasedate), aniDbFile.ReleaseDate?.ToTable());
        anidb.Add(nameof(AniDb.releasegroup), ReleaseGroupToDict(aniDbFile.ReleaseGroup));
        var mediadict = new Dictionary<string, object>();
        mediadict.Add(nameof(AniDbMedia.sublanguages), aniDbFile.MediaInfo.SubLanguages.Select(l => l.ToString()).ToList());
        mediadict.Add(nameof(AniDbMedia.dublanguages), aniDbFile.MediaInfo.AudioLanguages.Select(l => l.ToString()).ToList());
        anidb.Add(nameof(AniDb.media), mediadict);
        anidb.Add(nameof(AniDb.description), aniDbFile.Description);
        return anidb;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? ReleaseGroupToDict(IReleaseGroup? releaseGroup)
    {
        if (releaseGroup is null || releaseGroup.ID == 0 || releaseGroup.Name == "raw/unknown")
            return null;
        var groupdict = new Dictionary<string, object?>();
        groupdict.Add(nameof(ReleaseGroup.name), releaseGroup.Name);
        groupdict.Add(nameof(ReleaseGroup.shortname), releaseGroup.ShortName);
        return groupdict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> EpisodeToDict(IEpisode episode, LuaFunction getNameFn)
    {
        if (_tableCache.TryGetValue((typeof(IEpisode), episode.ID), out var eObj))
            return eObj;
        var epdict = new Dictionary<string, object?>();
        epdict.Add(nameof(Episode.duration), episode.Runtime.TotalSeconds);
        epdict.Add(nameof(Episode.number), episode.EpisodeNumber);
        epdict.Add(nameof(Episode.type), episode.Type.ToString());
        epdict.Add(nameof(Episode.airdate), episode.AirDate?.ToTable());
        epdict.Add(nameof(Episode.animeid), episode.SeriesID);
        epdict.Add(nameof(Episode.id), episode.ID);
        epdict.Add(nameof(Episode.titles), episode.Titles.Select(TitleToDict));
        epdict.Add(nameof(Episode.getname), getNameFn);
        epdict.Add(nameof(Episode.prefix), Utils.EpPrefix[episode.Type]);
        epdict.Add(nameof(Episode._classid), Episode._classidVal);
        _tableCache[(typeof(IEpisode), episode.ID)] = epdict;
        return epdict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, string?> TitleToDict(AnimeTitle title)
    {
        var titleDict = new Dictionary<string, string?>();
        titleDict.Add(nameof(Title.name), title.Title);
        titleDict.Add(nameof(Title.language), title.Language.ToString());
        titleDict.Add(nameof(Title.languagecode), title.LanguageCode);
        titleDict.Add(nameof(Title.type), title.Type.ToString());
        return titleDict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> FileToDict(IVideoFile file)
    {
        var fileDict = new Dictionary<string, object?>();
        fileDict.Add(nameof(LuaEnv.File.name), Path.GetFileNameWithoutExtension(file.FileName));
        fileDict.Add(nameof(LuaEnv.File.extension), Path.GetExtension(file.FileName));
        fileDict.Add(nameof(LuaEnv.File.path), file.Path);
        fileDict.Add(nameof(LuaEnv.File.size), file.Size);
        fileDict.Add(nameof(LuaEnv.File.earliestname), Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName));
        var hashdict = new Dictionary<string, object?>();
        hashdict.Add(nameof(Hashes.crc), file.Video.Hashes.CRC);
        hashdict.Add(nameof(Hashes.md5), file.Video.Hashes.MD5);
        hashdict.Add(nameof(Hashes.ed2k), file.Video.Hashes.ED2K);
        hashdict.Add(nameof(Hashes.sha1), file.Video.Hashes.SHA1);
        fileDict.Add(nameof(LuaEnv.File.hashes), hashdict);
        fileDict.Add(nameof(LuaEnv.File.anidb), AniDbFileToDict(file.Video.AniDB));
        fileDict.Add(nameof(LuaEnv.File.media), MediaInfoToDict(file.Video.MediaInfo));
        fileDict.Add(nameof(LuaEnv.File.importfolder), ImportFolderToDict(file.ImportFolder));
        return fileDict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> ImportFolderToDict(IImportFolder folder)
    {
        if (_tableCache.TryGetValue((typeof(IImportFolder), folder.ID), out var eObj))
            return eObj;
        var importdict = new Dictionary<string, object?>();
        importdict.Add(nameof(ImportFolder.id), folder.ID);
        importdict.Add(nameof(ImportFolder.name), folder.Name);
        importdict.Add(nameof(ImportFolder.location), folder.Path);
        importdict.Add(nameof(ImportFolder.type), folder.DropFolderType.ToString());
        importdict.Add(nameof(ImportFolder._classid), ImportFolder._classidVal);
        _tableCache[(typeof(IImportFolder), folder.ID)] = importdict;
        return importdict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? MediaInfoToDict(IMediaInfo? mediaInfo)
    {
        if (mediaInfo is null)
            return null;
        var mediainfo = new Dictionary<string, object?>();
        mediainfo.Add(nameof(Media.chaptered), mediaInfo.Chapters.Any());
        Dictionary<string, object>? videodict = null;
        if (mediaInfo.VideoStream is { } video)
        {
            videodict = new Dictionary<string, object>();
            videodict.Add(nameof(Video.height), video.Height);
            videodict.Add(nameof(Video.width), video.Width);
            videodict.Add(nameof(Video.codec), video.Codec.Simplified);
            videodict.Add(nameof(Video.res), video.Resolution);
            videodict.Add(nameof(Video.bitrate), video.BitRate);
            videodict.Add(nameof(Video.bitdepth), video.BitDepth);
            videodict.Add(nameof(Video.framerate), video.FrameRate);
        }

        mediainfo.Add(nameof(Media.video), videodict);
        mediainfo.Add(nameof(Media.duration), mediaInfo.Duration);
        mediainfo.Add(nameof(Media.bitrate), mediaInfo.BitRate);
        mediainfo.Add(nameof(Media.sublanguages), mediaInfo.TextStreams.Select(s => s.Language.ToString()).ToList());
        mediainfo.Add(nameof(Media.audio), mediaInfo.AudioStreams.Select(a =>
        {
            var audiodict = new Dictionary<string, object?>();
            audiodict.Add(nameof(Audio.compressionmode), a.CompressionMode);
            audiodict.Add(nameof(Audio.channels),
                !string.IsNullOrWhiteSpace(a.ChannelLayout) && a.ChannelLayout.Contains("LFE") ? a.Channels - 1 + 0.1 : a.Channels);
            audiodict.Add(nameof(Audio.samplingrate), a.SamplingRate);
            audiodict.Add(nameof(Audio.codec), a.Codec.Simplified);
            audiodict.Add(nameof(Audio.language), a.Language.ToString());
            audiodict.Add(nameof(Audio.title), a.Title);
            return audiodict;
        }).ToList());

        return mediainfo;
    }

    public LuaTable GetNewTable()
    {
        NewTable("_");
        return GetTable("_");
    }
}
