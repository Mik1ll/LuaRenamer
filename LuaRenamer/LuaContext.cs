﻿using System;
using System.Collections;
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
    private readonly Dictionary<(Type, int), LuaTable> _tableCache = new();


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
        var luaEnv = CreateLuaEnv(runSandboxed);
        var retVal = runSandboxed.Call(_args.Settings.Script, luaEnv);
        if (retVal.Length == 2 && (bool)retVal[0] != true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return GetTableDict(luaEnv);
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable CreateLuaEnv(LuaFunction runSandboxed)
    {
        var env = (LuaTable)DoString(BaseEnv)[0];
        env[nameof(Env.logdebug)] = RegisterFunction("_", this, LogDebugMethod);
        env[nameof(Env.log)] = RegisterFunction("_", this, LogMethod);
        env[nameof(Env.logwarn)] = RegisterFunction("_", this, LogWarnMethod);
        env[nameof(Env.logerror)] = RegisterFunction("_", this, LogErrorMethod);
        env[nameof(Env.episode_numbers)] = RegisterFunction("_", this, EpNumsMethod);
        runSandboxed.Call(_luaLinqText, env);
        runSandboxed.Call(_luaUtilsText, env);
        var getName = (LuaFunction)runSandboxed.Call(GetNameFunction, env)[1];

        var animes = _args.Series.OrderBy(s => s.AnidbAnimeID).Select(series => AnimeToTable(series.AnidbAnime, false, getName)).ToList();
        var episodes = _args.Episodes.Select(e => e.AnidbEpisode)
            .OrderBy(e => e.Type == EpisodeType.Other ? int.MinValue : (int)e.Type)
            .ThenBy(e => e.EpisodeNumber)
            .Select(e => EpisodeToTable(e, getName)).ToList();
        var groups = _args.Groups.Select(g => GroupToTable(g, getName)).ToList();

        env[nameof(Env.replace_illegal_chars)] = false;
        env[nameof(Env.remove_illegal_chars)] = false;
        env[nameof(Env.use_existing_anime_location)] = false;
        env[nameof(Env.skip_rename)] = false;
        env[nameof(Env.skip_move)] = false;
        env[nameof(Env.animes)] = GetNewArray(animes);
        env[nameof(Env.anime)] = animes.First();
        env[nameof(Env.file)] = FileToTable(_args.File);
        env[nameof(Env.episodes)] = GetNewArray(episodes);
        env[nameof(Env.episode)] = episodes.First();
        env[nameof(Env.importfolders)] = GetNewArray(_args.AvailableFolders.Select(ImportFolderToTable));
        env[nameof(Env.groups)] = GetNewArray(groups);
        env[nameof(Env.group)] = groups.FirstOrDefault();
        env[nameof(Env.AnimeType)] = EnumToTable<AnimeType>();
        env[nameof(Env.TitleType)] = EnumToTable<TitleType>();
        env[nameof(Env.Language)] = EnumToTable<TitleLanguage>();
        env[nameof(Env.EpisodeType)] = EnumToTable<EpisodeType>();
        env[nameof(Env.ImportFolderType)] = EnumToTable<DropFolderType>();
        env[nameof(Env.RelationType)] = EnumToTable<RelationType>();
        return env;
    }

    private LuaTable EnumToTable<T>() where T : struct, Enum
    {
        var enumTable = GetNewTable();
        foreach (var name in Enum.GetNames<T>())
            enumTable[name] = name;
        return enumTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable GroupToTable(IShokoGroup group, LuaFunction getNameFn)
    {
        var groupTable = GetNewTable();
        groupTable[nameof(Group.name)] = group.PreferredTitle;
        groupTable[nameof(Group.mainanime)] = AnimeToTable(group.MainSeries.AnidbAnime, false, getNameFn);
        groupTable[nameof(Group.animes)] = GetNewArray(group.AllSeries.Select(a => AnimeToTable(a.AnidbAnime, false, getNameFn)));
        return groupTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable AnimeToTable(ISeries anime, bool ignoreRelations, LuaFunction getNameFn)
    {
        if (anime == null) throw new ArgumentNullException(nameof(anime));
        if (_tableCache.TryGetValue((typeof(ISeries), anime.ID), out var eObj))
            return eObj;
        var series = anime.ShokoSeries.FirstOrDefault();
        var animeTable = GetNewTable();
        animeTable[nameof(Anime.airdate)] = DateTimeToTable(anime.AirDate);
        animeTable[nameof(Anime.enddate)] = DateTimeToTable(anime.EndDate);
        animeTable[nameof(Anime.rating)] = anime.Rating;
        animeTable[nameof(Anime.restricted)] = anime.Restricted;
        animeTable[nameof(Anime.type)] = anime.Type.ToString();
        animeTable[nameof(Anime.preferredname)] = string.IsNullOrWhiteSpace(series?.PreferredTitle) ? anime.PreferredTitle : series.PreferredTitle;
        animeTable[nameof(Anime.defaultname)] = string.IsNullOrWhiteSpace(series?.DefaultTitle) ? anime.DefaultTitle : series.DefaultTitle;
        animeTable[nameof(Anime.id)] = anime.ID;
        animeTable[nameof(Anime.titles)] = GetNewArray(anime.Titles.Select(TitleToTable));
        animeTable[nameof(Anime.getname)] = getNameFn;
        animeTable[nameof(Anime._classid)] = Anime._classidVal;
        var epCountTable = GetNewTable();
        epCountTable[EpisodeType.Episode.ToString()] = anime.EpisodeCounts.Episodes;
        epCountTable[EpisodeType.Special.ToString()] = anime.EpisodeCounts.Specials;
        epCountTable[EpisodeType.Credits.ToString()] = anime.EpisodeCounts.Credits;
        epCountTable[EpisodeType.Trailer.ToString()] = anime.EpisodeCounts.Trailers;
        epCountTable[EpisodeType.Other.ToString()] = anime.EpisodeCounts.Others;
        epCountTable[EpisodeType.Parody.ToString()] = anime.EpisodeCounts.Parodies;
        animeTable[nameof(Anime.episodecounts)] = epCountTable;
        animeTable[nameof(Anime.relations)] = GetNewArray(ignoreRelations
            ? []
            : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                .Select(r =>
                {
                    var relationTable = GetNewTable();
                    relationTable[nameof(Relation.type)] = r.RelationType.ToString();
                    relationTable[nameof(Relation.anime)] = AnimeToTable(r.Related!, true, getNameFn);
                    return relationTable;
                }));
        _tableCache[(typeof(ISeries), anime.ID)] = animeTable;
        return animeTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable? AniDbFileToTable(IAniDBFile? aniDb)
    {
        if (aniDb is null)
            return null;
        var aniDbTable = GetNewTable();
        aniDbTable[nameof(AniDb.id)] = aniDb.AniDBFileID;
        aniDbTable[nameof(AniDb.censored)] = aniDb.Censored;
        aniDbTable[nameof(AniDb.source)] = aniDb.Source;
        aniDbTable[nameof(AniDb.version)] = aniDb.Version;
        aniDbTable[nameof(AniDb.releasedate)] = DateTimeToTable(aniDb.ReleaseDate);
        aniDbTable[nameof(AniDb.releasegroup)] = ReleaseGroupToTable(aniDb.ReleaseGroup);
        var mediaTable = GetNewTable();
        mediaTable[nameof(AniDbMedia.sublanguages)] = GetNewArray(aniDb.MediaInfo.SubLanguages.Select(l => l.ToString()));
        mediaTable[nameof(AniDbMedia.dublanguages)] = GetNewArray(aniDb.MediaInfo.AudioLanguages.Select(l => l.ToString()));
        aniDbTable[nameof(AniDb.media)] = mediaTable;
        aniDbTable[nameof(AniDb.description)] = aniDb.Description;
        return aniDbTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable? ReleaseGroupToTable(IReleaseGroup? releaseGroup)
    {
        if (releaseGroup is null || releaseGroup.ID == 0 || releaseGroup.Name == "raw/unknown")
            return null;
        var groupTable = GetNewTable();
        groupTable[nameof(ReleaseGroup.name)] = releaseGroup.Name;
        groupTable[nameof(ReleaseGroup.shortname)] = releaseGroup.ShortName;
        return groupTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable EpisodeToTable(IEpisode episode, LuaFunction getNameFn)
    {
        if (_tableCache.TryGetValue((typeof(IEpisode), episode.ID), out var eObj))
            return eObj;
        var epTable = GetNewTable();
        epTable[nameof(Episode.duration)] = episode.Runtime.TotalSeconds;
        epTable[nameof(Episode.number)] = episode.EpisodeNumber;
        epTable[nameof(Episode.type)] = episode.Type.ToString();
        epTable[nameof(Episode.airdate)] = DateTimeToTable(episode.AirDate);
        epTable[nameof(Episode.animeid)] = episode.SeriesID;
        epTable[nameof(Episode.id)] = episode.ID;
        epTable[nameof(Episode.titles)] = GetNewArray(episode.Titles.Select(TitleToTable));
        epTable[nameof(Episode.getname)] = getNameFn;
        epTable[nameof(Episode.prefix)] = Utils.EpPrefix[episode.Type];
        epTable[nameof(Episode._classid)] = Episode._classidVal;
        _tableCache[(typeof(IEpisode), episode.ID)] = epTable;
        return epTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable TitleToTable(AnimeTitle title)
    {
        var titleTable = GetNewTable();
        titleTable[nameof(Title.name)] = title.Title;
        titleTable[nameof(Title.language)] = title.Language.ToString();
        titleTable[nameof(Title.languagecode)] = title.LanguageCode;
        titleTable[nameof(Title.type)] = title.Type.ToString();
        return titleTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable FileToTable(IVideoFile file)
    {
        var fileTable = GetNewTable();
        fileTable[nameof(LuaEnv.File.name)] = Path.GetFileNameWithoutExtension(file.FileName);
        fileTable[nameof(LuaEnv.File.extension)] = Path.GetExtension(file.FileName);
        fileTable[nameof(LuaEnv.File.path)] = file.Path;
        fileTable[nameof(LuaEnv.File.size)] = file.Size;
        fileTable[nameof(LuaEnv.File.earliestname)] = Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName);
        var hashTable = GetNewTable();
        hashTable[nameof(Hashes.crc)] = file.Video.Hashes.CRC;
        hashTable[nameof(Hashes.md5)] = file.Video.Hashes.MD5;
        hashTable[nameof(Hashes.ed2k)] = file.Video.Hashes.ED2K;
        hashTable[nameof(Hashes.sha1)] = file.Video.Hashes.SHA1;
        fileTable[nameof(LuaEnv.File.hashes)] = hashTable;
        fileTable[nameof(LuaEnv.File.anidb)] = AniDbFileToTable(file.Video.AniDB);
        fileTable[nameof(LuaEnv.File.media)] = MediaInfoToTable(file.Video.MediaInfo);
        fileTable[nameof(LuaEnv.File.importfolder)] = ImportFolderToTable(file.ImportFolder);
        return fileTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable ImportFolderToTable(IImportFolder folder)
    {
        if (_tableCache.TryGetValue((typeof(IImportFolder), folder.ID), out var eObj))
            return eObj;
        var importTable = GetNewTable();
        importTable[nameof(ImportFolder.id)] = folder.ID;
        importTable[nameof(ImportFolder.name)] = folder.Name;
        importTable[nameof(ImportFolder.location)] = folder.Path;
        importTable[nameof(ImportFolder.type)] = folder.DropFolderType.ToString();
        importTable[nameof(ImportFolder._classid)] = ImportFolder._classidVal;
        _tableCache[(typeof(IImportFolder), folder.ID)] = importTable;
        return importTable;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable? MediaInfoToTable(IMediaInfo? mediaInfo)
    {
        if (mediaInfo is null)
            return null;
        var mediaInfoTable = GetNewTable();
        mediaInfoTable[nameof(Media.chaptered)] = mediaInfo.Chapters.Any();
        if (mediaInfo.VideoStream is { } video)
        {
            var videoTable = GetNewTable();
            videoTable[nameof(Video.height)] = video.Height;
            videoTable[nameof(Video.width)] = video.Width;
            videoTable[nameof(Video.codec)] = video.Codec.Simplified;
            videoTable[nameof(Video.res)] = video.Resolution;
            videoTable[nameof(Video.bitrate)] = video.BitRate;
            videoTable[nameof(Video.bitdepth)] = video.BitDepth;
            videoTable[nameof(Video.framerate)] = video.FrameRate;
            mediaInfoTable[nameof(Media.video)] = videoTable;
        }

        mediaInfoTable[nameof(Media.duration)] = mediaInfo.Duration;
        mediaInfoTable[nameof(Media.bitrate)] = mediaInfo.BitRate;
        mediaInfoTable[nameof(Media.sublanguages)] = GetNewArray(mediaInfo.TextStreams.Select(s => s.Language.ToString()));
        mediaInfoTable[nameof(Media.audio)] = GetNewArray(mediaInfo.AudioStreams.Select(a =>
        {
            var audioTable = GetNewTable();
            audioTable[nameof(Audio.compressionmode)] = a.CompressionMode;
            audioTable[nameof(Audio.channels)] =
                !string.IsNullOrWhiteSpace(a.ChannelLayout) && a.ChannelLayout.Contains("LFE") ? a.Channels - 1 + 0.1 : a.Channels;
            audioTable[nameof(Audio.samplingrate)] = a.SamplingRate;
            audioTable[nameof(Audio.codec)] = a.Codec.Simplified;
            audioTable[nameof(Audio.language)] = a.Language.ToString();
            audioTable[nameof(Audio.title)] = a.Title;
            return audioTable;
        }));

        return mediaInfoTable;
    }

    private LuaTable? DateTimeToTable(DateTime? dateTime)
    {
        if (dateTime is not { } dt)
            return null;
        var dateTimeTable = GetNewTable();
        dateTimeTable[nameof(Date.year)] = dt.Year;
        dateTimeTable[nameof(Date.month)] = dt.Month;
        dateTimeTable[nameof(Date.day)] = dt.Day;
        dateTimeTable[nameof(Date.yday)] = dt.DayOfYear;
        dateTimeTable[nameof(Date.wday)] = (long)dt.DayOfWeek + 1;
        dateTimeTable[nameof(Date.hour)] = dt.Hour;
        dateTimeTable[nameof(Date.min)] = dt.Minute;
        dateTimeTable[nameof(Date.sec)] = dt.Second;
        dateTimeTable[nameof(Date.isdst)] = dt.IsDaylightSavingTime();
        return dateTimeTable;
    }

    private LuaTable GetNewTable()
    {
        NewTable("_");
        return GetTable("_");
    }

    private LuaTable GetNewArray(IEnumerable list)
    {
        var table = GetNewTable();
        var i = 1;
        foreach (var item in list)
            table[i++] = item;
        return table;
    }
}
