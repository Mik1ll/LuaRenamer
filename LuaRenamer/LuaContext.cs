using System;
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
              local name = from(self.{{nameof(AnimeTable.titles)}}):where(function(t1) ---@param t1 Title
                  return t1.{{nameof(TitleTable.language)}} == lang and title_priority[t1.{{nameof(TitleTable.type)}}] ~= nil
              end):orderby(function(t2) ---@param t2 Title
                  return title_priority[t2.{{nameof(TitleTable.type)}}]
              end):select("{{nameof(TitleTable.name)}}"):first()
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

    public LuaTable RunSandboxed()
    {
        var runSandboxed = (LuaFunction)DoString(SandboxFunction)[0];
        var luaEnv = CreateLuaEnv(runSandboxed);
        var retVal = runSandboxed.Call(_args.Settings.Script, luaEnv);
        if (retVal.Length == 2 && (bool)retVal[0] != true && retVal[1] is string errStr)
            throw new LuaRenamerException(errStr);
        return luaEnv;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private LuaTable CreateLuaEnv(LuaFunction runSandboxed)
    {
        var env = (LuaTable)DoString(BaseEnv)[0];
        env[nameof(EnvTable.logdebug)] = RegisterFunction("_", this, LogDebugMethod);
        env[nameof(EnvTable.log)] = RegisterFunction("_", this, LogMethod);
        env[nameof(EnvTable.logwarn)] = RegisterFunction("_", this, LogWarnMethod);
        env[nameof(EnvTable.logerror)] = RegisterFunction("_", this, LogErrorMethod);
        env[nameof(EnvTable.episode_numbers)] = RegisterFunction("_", this, EpNumsMethod);
        runSandboxed.Call(_luaLinqText, env);
        runSandboxed.Call(_luaUtilsText, env);
        var getName = (LuaFunction)runSandboxed.Call(GetNameFunction, env)[1];

        var animes = _args.Series.OrderBy(s => s.AnidbAnimeID).Select(series => AnimeToTable(series.AnidbAnime, false, getName)).ToList();
        var episodes = _args.Episodes.Select(e => e.AnidbEpisode)
            .OrderBy(e => e.Type == EpisodeType.Other ? int.MinValue : (int)e.Type)
            .ThenBy(e => e.EpisodeNumber)
            .Select(e => EpisodeToTable(e, getName)).ToList();
        var groups = _args.Groups.Select(g => GroupToTable(g, getName)).ToList();

        env[nameof(EnvTable.replace_illegal_chars)] = false;
        env[nameof(EnvTable.remove_illegal_chars)] = false;
        env[nameof(EnvTable.use_existing_anime_location)] = false;
        env[nameof(EnvTable.skip_rename)] = false;
        env[nameof(EnvTable.skip_move)] = false;
        env[nameof(EnvTable.animes)] = GetNewArray(animes);
        env[nameof(EnvTable.anime)] = animes.First();
        env[nameof(EnvTable.file)] = FileToTable(_args.File);
        env[nameof(EnvTable.episodes)] = GetNewArray(episodes);
        env[nameof(EnvTable.episode)] = episodes.First();
        env[nameof(EnvTable.importfolders)] = GetNewArray(_args.AvailableFolders.Select(ImportFolderToTable));
        env[nameof(EnvTable.groups)] = GetNewArray(groups);
        env[nameof(EnvTable.group)] = groups.FirstOrDefault();
        env[nameof(EnvTable.AnimeType)] = EnumToTable<AnimeType>();
        env[nameof(EnvTable.TitleType)] = EnumToTable<TitleType>();
        env[nameof(EnvTable.Language)] = EnumToTable<TitleLanguage>();
        env[nameof(EnvTable.EpisodeType)] = EnumToTable<EpisodeType>();
        env[nameof(EnvTable.ImportFolderType)] = EnumToTable<DropFolderType>();
        env[nameof(EnvTable.RelationType)] = EnumToTable<RelationType>();
        return env;
    }

    private LuaTable EnumToTable<T>() where T : struct, Enum
    {
        var enumTable = GetNewTable();
        foreach (var name in Enum.GetNames<T>())
            enumTable[name] = name;
        return enumTable;
    }

    private LuaTable GroupToTable(IShokoGroup group, LuaFunction getName)
    {
        var groupTable = GetNewTable();
        groupTable[nameof(GroupTable.name)] = group.PreferredTitle;
        groupTable[nameof(GroupTable.mainanime)] = AnimeToTable(group.MainSeries.AnidbAnime, false, getName);
        groupTable[nameof(GroupTable.animes)] = GetNewArray(group.AllSeries.Select(a => AnimeToTable(a.AnidbAnime, false, getName)));
        return groupTable;
    }

    private LuaTable AnimeToTable(ISeries anime, bool ignoreRelations, LuaFunction getName)
    {
        if (anime == null) throw new ArgumentNullException(nameof(anime));
        if (_tableCache.TryGetValue((typeof(ISeries), anime.ID), out var eObj))
            return eObj;
        var series = anime.ShokoSeries.FirstOrDefault();
        var animeTable = GetNewTable();
        animeTable[nameof(AnimeTable.airdate)] = DateTimeToTable(anime.AirDate);
        animeTable[nameof(AnimeTable.enddate)] = DateTimeToTable(anime.EndDate);
        animeTable[nameof(AnimeTable.rating)] = anime.Rating;
        animeTable[nameof(AnimeTable.restricted)] = anime.Restricted;
        animeTable[nameof(AnimeTable.type)] = anime.Type.ToString();
        animeTable[nameof(AnimeTable.preferredname)] = string.IsNullOrWhiteSpace(series?.PreferredTitle) ? anime.PreferredTitle : series.PreferredTitle;
        animeTable[nameof(AnimeTable.defaultname)] = string.IsNullOrWhiteSpace(series?.DefaultTitle) ? anime.DefaultTitle : series.DefaultTitle;
        animeTable[nameof(AnimeTable.id)] = anime.ID;
        animeTable[nameof(AnimeTable.titles)] = GetNewArray(anime.Titles.OrderBy(t => t.Title).Select(TitleToTable));
        animeTable[nameof(AnimeTable.getname)] = getName;
        animeTable[nameof(AnimeTable._classid)] = AnimeTable._classidVal;
        var epCountTable = GetNewTable();
        foreach (var epType in Enum.GetValues<EpisodeType>())
            epCountTable[epType.ToString()] = anime.EpisodeCounts[epType];
        animeTable[nameof(AnimeTable.episodecounts)] = epCountTable;
        animeTable[nameof(AnimeTable.relations)] = GetNewArray(ignoreRelations
            ? []
            : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                .Select(r => RelationToTable(r, getName)));
        _tableCache[(typeof(ISeries), anime.ID)] = animeTable;
        return animeTable;
    }

    private LuaTable RelationToTable(IRelatedMetadata<ISeries> relation, LuaFunction getName)
    {
        var relationTable = GetNewTable();
        relationTable[nameof(RelationTable.type)] = relation.RelationType.ToString();
        relationTable[nameof(RelationTable.anime)] = AnimeToTable(relation.Related!, true, getName);
        return relationTable;
    }

    private LuaTable? AniDbFileToTable(IAniDBFile? aniDb)
    {
        if (aniDb is null)
            return null;
        var aniDbTable = GetNewTable();
        aniDbTable[nameof(AniDbTable.id)] = aniDb.AniDBFileID;
        aniDbTable[nameof(AniDbTable.censored)] = aniDb.Censored;
        aniDbTable[nameof(AniDbTable.source)] = aniDb.Source;
        aniDbTable[nameof(AniDbTable.version)] = aniDb.Version;
        aniDbTable[nameof(AniDbTable.releasedate)] = DateTimeToTable(aniDb.ReleaseDate);
        aniDbTable[nameof(AniDbTable.releasegroup)] = ReleaseGroupToTable(aniDb.ReleaseGroup);
        var mediaTable = GetNewTable();
        mediaTable[nameof(AniDbMediaTable.sublanguages)] = GetNewArray(aniDb.MediaInfo.SubLanguages.Select(l => l.ToString()));
        mediaTable[nameof(AniDbMediaTable.dublanguages)] = GetNewArray(aniDb.MediaInfo.AudioLanguages.Select(l => l.ToString()));
        aniDbTable[nameof(AniDbTable.media)] = mediaTable;
        aniDbTable[nameof(AniDbTable.description)] = aniDb.Description;
        return aniDbTable;
    }

    private LuaTable? ReleaseGroupToTable(IReleaseGroup? releaseGroup)
    {
        if (releaseGroup is null || releaseGroup.ID == 0 || releaseGroup.Name == "raw/unknown")
            return null;
        var groupTable = GetNewTable();
        groupTable[nameof(ReleaseGroupTable.name)] = releaseGroup.Name;
        groupTable[nameof(ReleaseGroupTable.shortname)] = releaseGroup.ShortName;
        return groupTable;
    }

    private LuaTable EpisodeToTable(IEpisode episode, LuaFunction getName)
    {
        if (_tableCache.TryGetValue((typeof(IEpisode), episode.ID), out var eObj))
            return eObj;
        var epTable = GetNewTable();
        epTable[nameof(EpisodeTable.duration)] = episode.Runtime.TotalSeconds;
        epTable[nameof(EpisodeTable.number)] = episode.EpisodeNumber;
        epTable[nameof(EpisodeTable.type)] = episode.Type.ToString();
        epTable[nameof(EpisodeTable.airdate)] = DateTimeToTable(episode.AirDate);
        epTable[nameof(EpisodeTable.animeid)] = episode.SeriesID;
        epTable[nameof(EpisodeTable.id)] = episode.ID;
        epTable[nameof(EpisodeTable.titles)] = GetNewArray(episode.Titles.OrderBy(t => t.Title).Select(TitleToTable));
        epTable[nameof(EpisodeTable.getname)] = getName;
        epTable[nameof(EpisodeTable.prefix)] = Utils.EpPrefix[episode.Type];
        epTable[nameof(EpisodeTable._classid)] = EpisodeTable._classidVal;
        _tableCache[(typeof(IEpisode), episode.ID)] = epTable;
        return epTable;
    }

    private LuaTable TitleToTable(AnimeTitle title)
    {
        var titleTable = GetNewTable();
        titleTable[nameof(TitleTable.name)] = title.Title;
        titleTable[nameof(TitleTable.language)] = title.Language.ToString();
        titleTable[nameof(TitleTable.languagecode)] = title.LanguageCode;
        titleTable[nameof(TitleTable.type)] = title.Type.ToString();
        return titleTable;
    }

    private LuaTable FileToTable(IVideoFile file)
    {
        var fileTable = GetNewTable();
        fileTable[nameof(FileTable.name)] = Path.GetFileNameWithoutExtension(file.FileName);
        fileTable[nameof(FileTable.extension)] = Path.GetExtension(file.FileName);
        fileTable[nameof(FileTable.path)] = file.Path;
        fileTable[nameof(FileTable.size)] = file.Size;
        fileTable[nameof(FileTable.earliestname)] = Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName);
        var hashTable = GetNewTable();
        hashTable[nameof(HashesTable.crc)] = file.Video.Hashes.CRC;
        hashTable[nameof(HashesTable.md5)] = file.Video.Hashes.MD5;
        hashTable[nameof(HashesTable.ed2k)] = file.Video.Hashes.ED2K;
        hashTable[nameof(HashesTable.sha1)] = file.Video.Hashes.SHA1;
        fileTable[nameof(FileTable.hashes)] = hashTable;
        fileTable[nameof(FileTable.anidb)] = AniDbFileToTable(file.Video.AniDB);
        fileTable[nameof(FileTable.media)] = MediaInfoToTable(file.Video.MediaInfo);
        fileTable[nameof(FileTable.importfolder)] = ImportFolderToTable(file.ImportFolder);
        return fileTable;
    }

    private LuaTable ImportFolderToTable(IImportFolder folder)
    {
        if (_tableCache.TryGetValue((typeof(IImportFolder), folder.ID), out var eObj))
            return eObj;
        var importTable = GetNewTable();
        importTable[nameof(ImportFolderTable.id)] = folder.ID;
        importTable[nameof(ImportFolderTable.name)] = folder.Name;
        importTable[nameof(ImportFolderTable.location)] = folder.Path;
        importTable[nameof(ImportFolderTable.type)] = folder.DropFolderType.ToString();
        importTable[nameof(ImportFolderTable._classid)] = ImportFolderTable._classidVal;
        _tableCache[(typeof(IImportFolder), folder.ID)] = importTable;
        return importTable;
    }

    private LuaTable? MediaInfoToTable(IMediaInfo? mediaInfo)
    {
        if (mediaInfo is null)
            return null;
        var mediaInfoTable = GetNewTable();
        mediaInfoTable[nameof(MediaTable.chaptered)] = mediaInfo.Chapters.Any();
        if (mediaInfo.VideoStream is { } video)
        {
            var videoTable = GetNewTable();
            videoTable[nameof(VideoTable.height)] = video.Height;
            videoTable[nameof(VideoTable.width)] = video.Width;
            videoTable[nameof(VideoTable.codec)] = video.Codec.Simplified;
            videoTable[nameof(VideoTable.res)] = video.Resolution;
            videoTable[nameof(VideoTable.bitrate)] = video.BitRate;
            videoTable[nameof(VideoTable.bitdepth)] = video.BitDepth;
            videoTable[nameof(VideoTable.framerate)] = video.FrameRate;
            mediaInfoTable[nameof(MediaTable.video)] = videoTable;
        }

        mediaInfoTable[nameof(MediaTable.duration)] = mediaInfo.Duration;
        mediaInfoTable[nameof(MediaTable.bitrate)] = mediaInfo.BitRate;
        mediaInfoTable[nameof(MediaTable.sublanguages)] = GetNewArray(mediaInfo.TextStreams.Select(s => s.Language.ToString()));
        mediaInfoTable[nameof(MediaTable.audio)] = GetNewArray(mediaInfo.AudioStreams.Select(a =>
        {
            var audioTable = GetNewTable();
            audioTable[nameof(AudioTable.compressionmode)] = a.CompressionMode;
            audioTable[nameof(AudioTable.channels)] =
                !string.IsNullOrWhiteSpace(a.ChannelLayout) && a.ChannelLayout.Contains("LFE") ? a.Channels - 1 + 0.1 : a.Channels;
            audioTable[nameof(AudioTable.samplingrate)] = a.SamplingRate;
            audioTable[nameof(AudioTable.codec)] = a.Codec.Simplified;
            audioTable[nameof(AudioTable.language)] = a.Language.ToString();
            audioTable[nameof(AudioTable.title)] = a.Title;
            return audioTable;
        }));

        return mediaInfoTable;
    }

    private LuaTable? DateTimeToTable(DateTime? dateTime)
    {
        if (dateTime is not { } dt)
            return null;
        var dateTimeTable = GetNewTable();
        dateTimeTable[nameof(DateTable.year)] = dt.Year;
        dateTimeTable[nameof(DateTable.month)] = dt.Month;
        dateTimeTable[nameof(DateTable.day)] = dt.Day;
        dateTimeTable[nameof(DateTable.yday)] = dt.DayOfYear;
        dateTimeTable[nameof(DateTable.wday)] = (long)dt.DayOfWeek + 1;
        dateTimeTable[nameof(DateTable.hour)] = dt.Hour;
        dateTimeTable[nameof(DateTable.min)] = dt.Minute;
        dateTimeTable[nameof(DateTable.sec)] = dt.Second;
        dateTimeTable[nameof(DateTable.isdst)] = dt.IsDaylightSavingTime();
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
