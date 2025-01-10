﻿using System;
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
using Shoko.Plugin.Abstractions.Events;

namespace LuaRenamer;

public class LuaContext : Lua
{
    private readonly ILogger _logger;
    private readonly RelocationEventArgs<LuaRenamerSettings> _args;
    private static readonly Stopwatch FileCacheStopwatch = new();
    private static string? _luaUtilsText;
    private static string? _luaLinqText;
    public static readonly string LuaPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua");


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
              local name = from(self.{{nameof(Env.anime.titles)}}):where(function(t1) ---@param t1 Title
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
        var runSandboxFn = (LuaFunction)DoString(SandboxFunction)[0];
        var luaEnv = (LuaTable)DoString(BaseEnv)[0];

        luaEnv[Env.Inst.logdebug] = RegisterFunction("_", this, LogDebugMethod);
        luaEnv[Env.Inst.log] = RegisterFunction("_", this, LogMethod);
        luaEnv[Env.Inst.logwarn] = RegisterFunction("_", this, LogWarnMethod);
        luaEnv[Env.Inst.logerror] = RegisterFunction("_", this, LogErrorMethod);
        luaEnv[Env.Inst.episode_numbers] = RegisterFunction("_", this, EpNumsMethod);
        runSandboxFn.Call(_luaLinqText, luaEnv);
        runSandboxFn.Call(_luaUtilsText, luaEnv);
        var getNameFn = (LuaFunction)runSandboxFn.Call(GetNameFunction, luaEnv)[1];
        var env = CreateLuaEnv(getNameFn);
        foreach (var (k, v) in env) this.AddObject(luaEnv, v, k);
        var retVal = runSandboxFn.Call(_args.Settings.Script, luaEnv);
        if (retVal.Length == 2 && (bool)retVal[0] != true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return GetTableDict(luaEnv);
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> CreateLuaEnv(LuaFunction getNameFn)
    {
        Dictionary<int, Dictionary<string, object?>> animeCache = new();

        var animes = _args.Series.Select(series => AnimeToDict(series.AnidbAnime, animeCache, false, getNameFn)).ToList();
        var anidb = AniDbFileToDict();
        var mediainfo = MediaInfoToDict();
        var importfolders = _args.AvailableFolders.Select(ImportFolderToDict).ToList();
        var file = FileToDict(anidb, mediainfo);
        var episodes = EpisodesToDict(getNameFn);
        var groups = GroupsToDict(animeCache, getNameFn);

        var env = new Dictionary<string, object?>();
        env.Add(Env.Inst.filename, null);
        env.Add(Env.Inst.destination, null);
        env.Add(Env.Inst.subfolder, null);
        env.Add(Env.Inst.replace_illegal_chars, false);
        env.Add(Env.Inst.remove_illegal_chars, false);
        env.Add(Env.Inst.use_existing_anime_location, false);
        env.Add(Env.Inst.skip_rename, false);
        env.Add(Env.Inst.skip_move, false);
        env.Add(Env.Inst.animes, animes);
        env.Add(Env.anime.N, animes.First());
        env.Add(Env.file.N, file);
        env.Add(Env.Inst.episodes.Fn, episodes);
        env.Add(Env.Inst.episode.Fn, episodes.Where(e => (int)e[nameof(Episode.animeid)]! == (int)animes.First()[Env.anime.id]!)
            .OrderBy(e => (string)e[nameof(Episode.type)]! == EpisodeType.Other.ToString()
                ? int.MinValue
                : (int)Enum.Parse<EpisodeType>((string)e[nameof(Episode.type)]!))
            .ThenBy(e => (int)e[nameof(Episode.number)]!)
            .First());
        env.Add(Env.Inst.importfolders.Fn, importfolders);
        env.Add(Env.Inst.groups.Fn, groups);
        env.Add(Env.Inst.group.Fn, groups.FirstOrDefault());
        env.Add(Env.Inst.AnimeType, EnumToDict<AnimeType>());
        env.Add(Env.Inst.TitleType, EnumToDict<TitleType>());
        env.Add(Env.Inst.Language, EnumToDict<TitleLanguage>());
        env.Add(Env.Inst.EpisodeType, EnumToDict<EpisodeType>());
        env.Add(Env.Inst.ImportFolderType, EnumToDict<DropFolderType>());
        env.Add(Env.Inst.RelationType, EnumToDict<RelationType>());
        return env;
    }

    private Dictionary<string, string> EnumToDict<T>() => Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a!.ToString()!, a => a!.ToString()!);

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, object?>> GroupsToDict(Dictionary<int, Dictionary<string, object?>> animeCache, LuaFunction getNameFn)
    {
        var groups = _args.Groups.Select(g =>
        {
            var groupdict = new Dictionary<string, object?>();
            groupdict.Add(nameof(Group.name), g.PreferredTitle);
            groupdict.Add(nameof(Group.mainanime), AnimeToDict(g.MainSeries.AnidbAnime, animeCache, false, getNameFn));
            groupdict.Add(nameof(Group.animes), g.AllSeries.Select(a => AnimeToDict(a.AnidbAnime, animeCache, false, getNameFn)).ToList());
            return groupdict;
        }).ToList();
        return groups;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> AnimeToDict(ISeries anime, Dictionary<int, Dictionary<string, object?>> animeCache, bool ignoreRelations,
        LuaFunction getNameFn)
    {
        if (anime == null) throw new ArgumentNullException(nameof(anime));
        if (animeCache.TryGetValue(anime.ID, out var animedict)) return animedict;
        var series = _args.Series.FirstOrDefault(series => series.AnidbAnime == anime);
        animedict = new Dictionary<string, object?>();
        animedict.Add(Env.anime.airdate, anime.AirDate?.ToTable());
        animedict.Add(Env.anime.enddate, anime.EndDate?.ToTable());
        animedict.Add(Env.anime.rating, anime.Rating);
        animedict.Add(Env.anime.restricted, anime.Restricted);
        animedict.Add(Env.anime.type, anime.Type.ToString());
        animedict.Add(Env.anime.preferredname, series?.PreferredTitle ?? anime.PreferredTitle);
        animedict.Add(Env.anime.defaultname, string.IsNullOrWhiteSpace(series?.DefaultTitle) ? anime.DefaultTitle : series.DefaultTitle);
        animedict.Add(Env.anime.id, anime.ID);
        animedict.Add(nameof(Env.anime.titles), ConvertTitles(anime.Titles));
        animedict.Add(Env.anime.getname, getNameFn);
        animedict.Add(Env.anime._classid, Env.anime._classidVal);
        var epcountdict = new Dictionary<string, int>();
        epcountdict.Add(EpisodeType.Episode.ToString(), anime.EpisodeCounts.Episodes);
        epcountdict.Add(EpisodeType.Special.ToString(), anime.EpisodeCounts.Specials);
        epcountdict.Add(EpisodeType.Credits.ToString(), anime.EpisodeCounts.Credits);
        epcountdict.Add(EpisodeType.Trailer.ToString(), anime.EpisodeCounts.Trailers);
        epcountdict.Add(EpisodeType.Other.ToString(), anime.EpisodeCounts.Others);
        epcountdict.Add(EpisodeType.Parody.ToString(), anime.EpisodeCounts.Parodies);
        animedict.Add(Env.anime.episodecounts, epcountdict);
        animedict.Add(Env.anime.relations.N, ignoreRelations
            ? new List<Dictionary<string, object?>>()
            : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                .Select(r =>
                {
                    var relationdict = new Dictionary<string, object?>();
                    relationdict.Add(Env.anime.relations.type, r.RelationType.ToString());
                    relationdict.Add(Env.anime.relations.anime, AnimeToDict(r.Related!, animeCache, true, getNameFn));
                    return relationdict;
                }).ToList());
        return animeCache[anime.ID] = animedict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? AniDbFileToDict()
    {
        Dictionary<string, object?>? anidb = null;
        if (_args.File.Video.AniDB is { } aniDbInfo)
        {
            anidb = new Dictionary<string, object?>();
            anidb.Add(Env.file.anidb.censored, aniDbInfo.Censored);
            anidb.Add(Env.file.anidb.source, aniDbInfo.Source);
            anidb.Add(Env.file.anidb.version, aniDbInfo.Version);
            anidb.Add(Env.file.anidb.releasedate, aniDbInfo.ReleaseDate?.ToTable());
            Dictionary<string, object?>? groupdict = null;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (aniDbInfo.ReleaseGroup is not null && aniDbInfo.ReleaseGroup.ID != 0 && aniDbInfo.ReleaseGroup.Name != "raw/unknown")
            {
                groupdict = new Dictionary<string, object?>();
                groupdict.Add(Env.file.anidb.releasegroup.name, aniDbInfo.ReleaseGroup.Name);
                groupdict.Add(Env.file.anidb.releasegroup.shortname, aniDbInfo.ReleaseGroup.ShortName);
            }

            anidb.Add(Env.file.anidb.releasegroup.N, groupdict);
            anidb.Add(Env.file.anidb.id, aniDbInfo.AniDBFileID);
            var mediadict = new Dictionary<string, object>();
            mediadict.Add(Env.file.anidb.media.sublanguages, aniDbInfo.MediaInfo.SubLanguages.Select(l => l.ToString()).ToList());
            mediadict.Add(Env.file.anidb.media.dublanguages, aniDbInfo.MediaInfo.AudioLanguages.Select(l => l.ToString()).ToList());
            anidb.Add(Env.file.anidb.media.N, mediadict);
            anidb.Add(Env.file.anidb.description, aniDbInfo.Description);
        }

        return anidb;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, object?>> EpisodesToDict(LuaFunction getNameFn)
    {
        var episodes = _args.Episodes.Select(se => se.AnidbEpisode).Select(e =>
        {
            var epdict = new Dictionary<string, object?>();
            epdict.Add(nameof(Episode.duration), e.Runtime.TotalSeconds);
            epdict.Add(nameof(Episode.number), e.EpisodeNumber);
            epdict.Add(nameof(Episode.type), e.Type.ToString());
            epdict.Add(nameof(Episode.airdate), e.AirDate?.ToTable());
            epdict.Add(nameof(Episode.animeid), e.SeriesID);
            epdict.Add(nameof(Episode.id), e.ID);
            epdict.Add(nameof(Episode.titles), ConvertTitles(e.Titles));
            epdict.Add(nameof(Episode.getname), getNameFn);
            epdict.Add(nameof(Episode.prefix), Utils.EpPrefix[e.Type]);
            epdict.Add(nameof(Episode._classid), Episode._classidVal);
            return epdict;
        }).ToList();
        return episodes;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, string?>> ConvertTitles(IEnumerable<AnimeTitle> titles)
    {
        return titles.Select(t =>
        {
            var title = new Dictionary<string, string?>();
            title.Add(nameof(Title.name), t.Title);
            title.Add(nameof(Title.language), t.Language.ToString());
            title.Add(nameof(Title.languagecode), t.LanguageCode);
            title.Add(nameof(Title.type), t.Type.ToString());
            return title;
        }).ToList();
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> FileToDict(Dictionary<string, object?>? anidb, Dictionary<string, object?>? mediainfo)
    {
        var file = new Dictionary<string, object?>();
        file.Add(Env.file.name, Path.GetFileNameWithoutExtension(_args.File.FileName));
        file.Add(Env.file.extension, Path.GetExtension(_args.File.FileName));
        file.Add(Env.file.path, _args.File.Path);
        file.Add(Env.file.size, _args.File.Size);
        file.Add(Env.file.earliestname, Path.GetFileNameWithoutExtension(_args.File.Video.EarliestKnownName));
        var hashdict = new Dictionary<string, object?>();
        hashdict.Add(Env.file.hashes.crc, _args.File.Video.Hashes.CRC);
        hashdict.Add(Env.file.hashes.md5, _args.File.Video.Hashes.MD5);
        hashdict.Add(Env.file.hashes.ed2k, _args.File.Video.Hashes.ED2K);
        hashdict.Add(Env.file.hashes.sha1, _args.File.Video.Hashes.SHA1);
        file.Add(Env.file.hashes.N, hashdict);
        file.Add(Env.file.anidb.N, anidb);
        file.Add(Env.file.media.N, mediainfo);
        file.Add(nameof(Env.file.importfolder), ImportFolderToDict(_args.File.ImportFolder));
        return file;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object> ImportFolderToDict(IImportFolder folder)
    {
        var importdict = new Dictionary<string, object>();
        importdict.Add(nameof(Importfolder.id), folder.ID);
        importdict.Add(nameof(Importfolder.name), folder.Name);
        importdict.Add(nameof(Importfolder.location), folder.Path);
        importdict.Add(nameof(Importfolder.type), folder.DropFolderType.ToString());
        importdict.Add(nameof(Importfolder._classid), Importfolder._classidVal);
        return importdict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? MediaInfoToDict()
    {
        Dictionary<string, object?>? mediainfo = null;
        if (_args.File.Video.MediaInfo is { } mediaInfo)
        {
            mediainfo = new Dictionary<string, object?>();
            mediainfo.Add(Env.file.media.chaptered, mediaInfo.Chapters.Any());
            Dictionary<string, object>? videodict = null;
            if (mediaInfo.VideoStream is { } video)
            {
                videodict = new Dictionary<string, object>();
                videodict.Add(Env.file.media.video.height, video.Height);
                videodict.Add(Env.file.media.video.width, video.Width);
                videodict.Add(Env.file.media.video.codec, video.Codec.Simplified);
                videodict.Add(Env.file.media.video.res, video.Resolution);
                videodict.Add(Env.file.media.video.bitrate, video.BitRate);
                videodict.Add(Env.file.media.video.bitdepth, video.BitDepth);
                videodict.Add(Env.file.media.video.framerate, video.FrameRate);
            }

            mediainfo.Add(Env.file.media.video.N, videodict);
            mediainfo.Add(Env.file.media.duration, mediaInfo.Duration);
            mediainfo.Add(Env.file.media.bitrate, mediaInfo.BitRate);
            mediainfo.Add(Env.file.media.sublanguages, mediaInfo.TextStreams.Select(s => s.Language.ToString()).ToList());
            mediainfo.Add(Env.file.media.audio.N, mediaInfo.AudioStreams.Select(a =>
            {
                var audiodict = new Dictionary<string, object?>();
                audiodict.Add(Env.file.media.audio.compressionmode, a.CompressionMode);
                audiodict.Add(Env.file.media.audio.channels,
                    !string.IsNullOrWhiteSpace(a.ChannelLayout) && a.ChannelLayout.Contains("LFE") ? a.Channels - 1 + 0.1 : a.Channels);
                audiodict.Add(Env.file.media.audio.samplingrate, a.SamplingRate);
                audiodict.Add(Env.file.media.audio.codec, a.Codec.Simplified);
                audiodict.Add(Env.file.media.audio.language, a.Language.ToString());
                audiodict.Add(Env.file.media.audio.title, a.Title);
                return audiodict;
            }).ToList());
        }

        return mediainfo;
    }
}
