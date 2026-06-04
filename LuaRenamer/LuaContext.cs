using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;
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
    private readonly Dictionary<(Type, int), LuaTable> _tableCache = new();
    private readonly IShokoSeries _primarySeries;
    private readonly IShokoEpisode _primaryEpisode;


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

    private string EpNums(int pad) => string.Join(' ', _args.Episodes.Select(se => se.AnidbEpisode)
        .Where(e => e.SeriesID == _primarySeries.AnidbAnimeID)
        .OrderBy(e => e.Type).ThenBy(e => e.EpisodeNumber)
        .Select((e, i) => (e.Type, RangeId: e.EpisodeNumber - i, Num: e.EpisodeNumber)) // RangeId effectively groups sequences of numbers
        .GroupBy(x => (x.Type, x.RangeId))
        .Select(g => g.First().Num is var fn && g.Last().Num is var ln && Utils.EpPrefix[g.Key.Type] is var pre && "D" + pad is var fmt && fn == ln
            ? $"{pre}{fn.ToString(fmt)}"
            : $"{pre}{fn.ToString(fmt)}-{ln.ToString(fmt)}"));

    private static readonly MethodInfo EpNumsMethod =
        typeof(LuaContext).GetMethod(nameof(EpNums), BindingFlags.Instance | BindingFlags.NonPublic)!;

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
        FillTable(env, [
            (nameof(EnvTable.logdebug), RegisterFunction("_", this, LogDebugMethod)),
            (nameof(EnvTable.log), RegisterFunction("_", this, LogMethod)),
            (nameof(EnvTable.logwarn), RegisterFunction("_", this, LogWarnMethod)),
            (nameof(EnvTable.logerror), RegisterFunction("_", this, LogErrorMethod)),
            (nameof(EnvTable.episode_numbers), RegisterFunction("_", this, EpNumsMethod)),
        ]);
        runSandboxed.Call(_luaLinqText, env);
        runSandboxed.Call(_luaUtilsText, env);
        var getName = (LuaFunction)runSandboxed.Call(GetNameFunction, env)[1];

        var animes = _args.Series
            .OrderBy(s => s.AnidbAnimeID != _primarySeries.AnidbAnimeID)
            .ThenBy(s => s.AnidbAnimeID)
            .Select(series => AnimeToTable(series.AnidbAnime, false, getName)).ToList();
        var episodes = _args.Episodes
            .OrderBy(e => e.AnidbEpisodeID != _primaryEpisode.AnidbEpisodeID)
            .ThenBy(e => e.AnidbEpisode.SeriesID)
            .ThenBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.AnidbEpisode.Type)
            .ThenBy(e => e.AnidbEpisode.EpisodeNumber)
            .Select(e => EpisodeToTable(e.AnidbEpisode, getName)).ToList();
        var groups = _args.Groups
            .OrderBy(g => g.MainSeriesID != _primarySeries.AnidbAnimeID)
            .Select(g => GroupToTable(g, getName)).ToList();

        return FillTable(env, [
            (nameof(EnvTable.replace_illegal_chars), _args.Configuration.ReplaceIllegalCharacters),
            (nameof(EnvTable.remove_illegal_chars), _args.Configuration.RemoveIllegalCharacters),
            (nameof(EnvTable.use_existing_anime_location), _args.Configuration.UseExistingAnimeLocation),
            (nameof(EnvTable.skip_rename), false),
            (nameof(EnvTable.skip_move), false),
            (nameof(EnvTable.illegal_chars_map), ReplaceMapToTable()),
            (nameof(EnvTable.animes), ArrayFrom(animes)),
            (nameof(EnvTable.anime), animes[0]),
            (nameof(EnvTable.file), FileToTable(_args.File)),
            (nameof(EnvTable.episodes), ArrayFrom(episodes)),
            (nameof(EnvTable.episode), episodes[0]),
            (nameof(EnvTable.importfolders), ArrayFrom(_args.AvailableFolders.Select(ImportFolderToTable))),
            (nameof(EnvTable.groups), ArrayFrom(groups)),
            (nameof(EnvTable.group), groups.FirstOrDefault()),
            (nameof(EnvTable.tmdb), TmdbToTable(getName)),
            (nameof(EnumsTable.AnimeType), EnumToTable<AnimeType>()),
            (nameof(EnumsTable.TitleType), EnumToTable<TitleType>()),
            (nameof(EnumsTable.Language), EnumToTable<TitleLanguage>()),
            (nameof(EnumsTable.EpisodeType), EnumToTable<EpisodeType>()),
            (nameof(EnumsTable.ImportFolderType), EnumToTable<DropFolderType>()),
            (nameof(EnumsTable.RelationType), EnumToTable<RelationType>()),
            (nameof(EnumsTable.SeasonName), EnumToTable<YearlySeason>()),
        ]);
    }

    // Use Distinct to prevent duplicate entries for enum values with the same underlying value
    private LuaTable EnumToTable<T>() where T : struct, Enum
        => TableFrom([.. Enum.GetValues<T>().Distinct().Select(v => (Enum.GetName(v)!, (object?)Enum.GetName(v)))]);

    private LuaTable ReplaceMapToTable()
        => TableFrom([.. FilePathCleaner.ReplaceMapDefaults.Select(kvp => (kvp.Key, (object?)kvp.Value))]);

    private LuaTable GroupToTable(IShokoGroup group, LuaFunction getName) => TableFrom([
        (nameof(GroupTable.name), group.Title),
        (nameof(GroupTable.mainanime), AnimeToTable(group.MainSeries.AnidbAnime, false, getName)),
        (nameof(GroupTable.animes), ArrayFrom(group.AllSeries.Select(a => AnimeToTable(a.AnidbAnime, false, getName)))),
    ]);

    private LuaTable AnimeToTable(IAnidbAnime anime, bool ignoreRelations, LuaFunction getName)
    {
        ArgumentNullException.ThrowIfNull(anime);
        if (GetCachedOrNewTable((typeof(IAnidbAnime), anime.ID), out var animeTable))
            return animeTable;
        var series = anime.ShokoSeries.FirstOrDefault();
        return FillTable(animeTable, [
            (nameof(AnimeTable.airdate), DateTimeToTable(anime.AirDate?.ToDateTime())),
            (nameof(AnimeTable.enddate), DateTimeToTable(anime.EndDate?.ToDateTime())),
            (nameof(AnimeTable.rating), anime.Rating),
            (nameof(AnimeTable.restricted), anime.Restricted),
            (nameof(AnimeTable.type), anime.Type.ToString()),
            (nameof(AnimeTable.preferredname), string.IsNullOrWhiteSpace(series?.Title) ? anime.Title : series.Title),
            (nameof(AnimeTable.defaultname), string.IsNullOrWhiteSpace(series?.DefaultTitle.Value) ? anime.DefaultTitle.Value : series.DefaultTitle.Value),
            (nameof(AnimeTable.id), anime.ID),
            (nameof(AnimeTable.titles), ArrayFrom(anime.Titles.OrderBy(t => t.Value).Select(TitleToTable))),
            (nameof(AnimeTable.getname), getName),
            (nameof(AnimeTable.studios), ArrayFrom(anime.Studios.Select(st => st.Name))),
            (nameof(AnimeTable._classid), AnimeTable._classidVal),
            (nameof(AnimeTable.episodecounts), TableFrom([..Enum.GetValues<EpisodeType>().Select(ep => (ep.ToString(), (object?)anime.EpisodeCounts[ep]))])),
            (nameof(AnimeTable.relations), ArrayFrom(ignoreRelations
                ? []
                : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                    .Select(r => RelationToTable(r, getName)))),
            (nameof(AnimeTable.tags), ArrayFrom(anime.Tags.Select(t => t.Name))),
            (nameof(AnimeTable.customtags), ArrayFrom(series?.Tags.Select(t => t.Name) ?? [])),
            (nameof(AnimeTable.seasons), ArrayFrom(anime.YearlySeasons.Select(SeasonToTable))),
        ]);
    }

    private LuaTable SeasonToTable((int Year, YearlySeason Season) season) => TableFrom([
        (nameof(SeasonTable.year), season.Year),
        (nameof(SeasonTable.season), Enum.GetName(season.Season)),
    ]);

    private LuaTable RelationToTable(IRelatedMetadata<ISeries, ISeries> relation, LuaFunction getName) => TableFrom([
        (nameof(RelationTable.type), relation.RelationType.ToString()),
        (nameof(RelationTable.anime), AnimeToTable((relation.Related as IAnidbAnime)!, true, getName)),
    ]);

    private LuaTable? AniDbFileToTable(IReleaseInfo? aniDb)
    {
        if (aniDb is not { ReleaseURI: var releaseUri } || !(releaseUri?.StartsWith("https://anidb.net/file/") ?? false))
            return null;
        return TableFrom([
            (nameof(AniDbTable.id), int.Parse(aniDb.ReleaseURI![23..])),
            (nameof(AniDbTable.censored), aniDb.IsCensored),
            (nameof(AniDbTable.source), Enum.GetName(aniDb.Source)),
            (nameof(AniDbTable.version), aniDb.Version),
            (nameof(AniDbTable.releasedate), DateTimeToTable(aniDb.ReleasedAt?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified))),
            (nameof(AniDbTable.releasegroup), ReleaseGroupToTable(aniDb.Group)),
            (nameof(AniDbTable.media), TableFrom([
                (nameof(AniDbMediaTable.sublanguages), ArrayFrom(aniDb.MediaInfo?.SubtitleLanguages.Select(l => l.ToString()) ?? [])),
                (nameof(AniDbMediaTable.dublanguages), ArrayFrom(aniDb.MediaInfo?.AudioLanguages.Select(l => l.ToString()) ?? [])),
            ])),
            (nameof(AniDbTable.description), aniDb.Comment),
        ]);
    }

    private LuaTable? ReleaseGroupToTable(IReleaseGroup? releaseGroup) =>
        releaseGroup?.ID is null || releaseGroup.Name == "raw/unknown" ? null : TableFrom([
            (nameof(ReleaseGroupTable.name), releaseGroup.Name),
            (nameof(ReleaseGroupTable.shortname), releaseGroup.ShortName),
        ]);

    private LuaTable EpisodeToTable(IAnidbEpisode episode, LuaFunction getName)
    {
        if (GetCachedOrNewTable((typeof(IAnidbEpisode), episode.ID), out var epTable))
            return epTable;
        return FillTable(epTable, [
            (nameof(EpisodeTable.duration), episode.Runtime.TotalSeconds),
            (nameof(EpisodeTable.number), episode.EpisodeNumber),
            (nameof(EpisodeTable.type), episode.Type.ToString()),
            (nameof(EpisodeTable.airdate), DateTimeToTable(episode.AirDateWithTime)),
            (nameof(EpisodeTable.animeid), episode.SeriesID),
            (nameof(EpisodeTable.id), episode.ID),
            (nameof(EpisodeTable.titles), ArrayFrom(episode.Titles.OrderBy(t => t.Value).Select(TitleToTable))),
            (nameof(EpisodeTable.getname), getName),
            (nameof(EpisodeTable.prefix), Utils.EpPrefix[episode.Type]),
            (nameof(EpisodeTable._classid), EpisodeTable._classidVal),
        ]);
    }

    private LuaTable TitleToTable(ITitle title) => TableFrom([
        (nameof(TitleTable.name), title.Value),
        (nameof(TitleTable.language), title.Language.ToString()),
        (nameof(TitleTable.languagecode), title.LanguageCode),
        (nameof(TitleTable.type), title.Type.ToString()),
    ]);

    private LuaTable FileToTable(IVideoFile file) => TableFrom([
        (nameof(FileTable.name), Path.GetFileNameWithoutExtension(file.FileName)),
        (nameof(FileTable.extension), Path.GetExtension(file.FileName)),
        (nameof(FileTable.path), file.Path),
        (nameof(FileTable.size), file.Size),
        (nameof(FileTable.earliestname), Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName)),
        (nameof(FileTable.hashes), TableFrom([
            (nameof(HashesTable.crc), file.Video.Hashes.FirstOrDefault(h => h.Type is "CRC32")?.Value),
            (nameof(HashesTable.md5), file.Video.Hashes.FirstOrDefault(h => h.Type is "MD5")?.Value),
            (nameof(HashesTable.ed2k), file.Video.ED2K),
            (nameof(HashesTable.sha1), file.Video.Hashes.FirstOrDefault(h => h.Type is "SHA1")?.Value),
        ])),
        (nameof(FileTable.anidb), AniDbFileToTable(file.Video.ReleaseInfo)),
        (nameof(FileTable.media), MediaInfoToTable(file.Video.MediaInfo)),
        (nameof(FileTable.importfolder), ImportFolderToTable(file.ManagedFolder)),
    ]);

    private LuaTable ImportFolderToTable(IManagedFolder folder)
    {
        if (GetCachedOrNewTable((typeof(IManagedFolder), folder.ID), out var importTable))
            return importTable;
        return FillTable(importTable, [
            (nameof(ImportFolderTable.id), folder.ID),
            (nameof(ImportFolderTable.name), folder.Name),
            (nameof(ImportFolderTable.location), folder.Path),
            (nameof(ImportFolderTable.type), folder.DropFolderType.ToString()),
            (nameof(ImportFolderTable._classid), ImportFolderTable._classidVal),
        ]);
    }

    private LuaTable? MediaInfoToTable(IMediaInfo? mediaInfo) => mediaInfo is null ? null : TableFrom([
        (nameof(MediaTable.video), mediaInfo.VideoStream is { } video ? TableFrom([
            (nameof(VideoTable.height), video.Height),
            (nameof(VideoTable.width), video.Width),
            (nameof(VideoTable.codec), video.Codec.Simplified),
            (nameof(VideoTable.res), video.Resolution),
            (nameof(VideoTable.bitrate), video.BitRate),
            (nameof(VideoTable.bitdepth), video.BitDepth),
            (nameof(VideoTable.framerate), video.FrameRate),
        ]) : null),
        (nameof(MediaTable.chaptered), mediaInfo.Chapters.Any()),
        (nameof(MediaTable.duration), mediaInfo.Duration),
        (nameof(MediaTable.bitrate), mediaInfo.BitRate),
        (nameof(MediaTable.sublanguages), ArrayFrom(mediaInfo.TextStreams.Select(s => s.Language.ToString()))),
        (nameof(MediaTable.audio), ArrayFrom(mediaInfo.AudioStreams.Select(a => TableFrom([
            (nameof(AudioTable.compressionmode), a.CompressionMode),
            (nameof(AudioTable.channels), !string.IsNullOrWhiteSpace(a.ChannelLayout) && a.ChannelLayout.Contains("LFE") ? a.Channels - 1 + 0.1 : a.Channels),
            (nameof(AudioTable.samplingrate), a.SamplingRate),
            (nameof(AudioTable.codec), a.Codec.Simplified),
            (nameof(AudioTable.language), a.Language.ToString()),
            (nameof(AudioTable.title), a.Title),
        ])))),
    ]);

    private LuaTable? DateTimeToTable(DateTime? dateTime) => dateTime is not { } dt ? null : TableFrom([
        (nameof(DateTimeTable.year), dt.Year),
        (nameof(DateTimeTable.month), dt.Month),
        (nameof(DateTimeTable.day), dt.Day),
        (nameof(DateTimeTable.yday), dt.DayOfYear),
        (nameof(DateTimeTable.wday), (long)dt.DayOfWeek + 1),
        (nameof(DateTimeTable.hour), dt.Hour),
        (nameof(DateTimeTable.min), dt.Minute),
        (nameof(DateTimeTable.sec), dt.Second),
        (nameof(DateTimeTable.isdst), dt.IsDaylightSavingTime()),
    ]);

    private LuaTable TmdbToTable(LuaFunction getName) => TableFrom([
        (nameof(TmdbTable.movies), ArrayFrom(_args.Series[0].TmdbMovies.Select(m => TableFrom([
            (nameof(TmdbMovieTable.id), m.ID),
            (nameof(TmdbMovieTable.titles), ArrayFrom(m.Titles.Select(TitleToTable))),
            (nameof(TmdbMovieTable.defaultname), m.DefaultTitle.Value),
            (nameof(TmdbMovieTable.preferredname), m.Title),
            (nameof(TmdbMovieTable.rating), m.Rating),
            (nameof(TmdbMovieTable.restricted), m.Restricted),
            (nameof(TmdbMovieTable.studios), ArrayFrom(m.Studios.Select(s => s.Name))),
            (nameof(TmdbMovieTable.airdate), DateTimeToTable(m.ReleaseDate)),
            (nameof(TmdbMovieTable.getname), getName),
        ])))),
        (nameof(TmdbTable.shows), ArrayFrom(_args.Series[0].TmdbShows.Select(s => TableFrom([
            (nameof(TmdbShowTable.id), s.ID),
            (nameof(TmdbShowTable.titles), ArrayFrom(s.Titles.Select(TitleToTable))),
            (nameof(TmdbShowTable.defaultname), s.DefaultTitle.Value),
            (nameof(TmdbShowTable.preferredname), s.Title),
            (nameof(TmdbShowTable.rating), s.Rating),
            (nameof(TmdbShowTable.restricted), s.Restricted),
            (nameof(TmdbShowTable.studios), ArrayFrom(s.Studios.Select(st => st.Name))),
            (nameof(TmdbShowTable.episodecount), s.EpisodeCounts.Episodes),
            (nameof(TmdbShowTable.airdate), DateTimeToTable(s.AirDate?.ToDateTime())),
            (nameof(TmdbShowTable.enddate), DateTimeToTable(s.EndDate?.ToDateTime())),
            (nameof(TmdbShowTable.getname), getName),
            (nameof(TmdbShowTable.seasons), ArrayFrom(s.YearlySeasons.Select(SeasonToTable))),
        ])))),
        (nameof(TmdbTable.episodes), ArrayFrom(_args.Episodes.Where(e => e.SeriesID == _primarySeries.ID)
            .SelectMany(e => e.TmdbEpisodes.Select(e2 => TableFrom([
                (nameof(TmdbEpisodeTable.showid), e2.SeriesID),
                (nameof(TmdbEpisodeTable.id), e2.ID),
                (nameof(TmdbEpisodeTable.titles), ArrayFrom(e2.Titles.Select(TitleToTable))),
                (nameof(TmdbEpisodeTable.defaultname), e2.DefaultTitle.Value),
                (nameof(TmdbEpisodeTable.preferredname), e2.Title),
                (nameof(TmdbEpisodeTable.type), e2.Type.ToString()),
                (nameof(TmdbEpisodeTable.number), e2.EpisodeNumber),
                (nameof(TmdbEpisodeTable.seasonnumber), e2.SeasonNumber),
                (nameof(TmdbEpisodeTable.airdate), DateTimeToTable(e2.AirDateWithTime)),
                (nameof(TmdbEpisodeTable.getname), getName),
            ]))))),
    ]);

    private static LuaTable FillTable(LuaTable table, params (string Key, object? Value)[] entries)
    {
        foreach (var (k, v) in entries) table[k] = v;
        return table;
    }

    private LuaTable TableFrom(params (string Key, object? Value)[] entries)
        => FillTable(GetNewTable(), entries);

    private LuaTable GetNewTable()
    {
        NewTable("_");
        return GetTable("_");
    }

    private LuaTable ArrayFrom(IEnumerable list)
    {
        var table = GetNewTable();
        var i = 1;
        foreach (var item in list)
            table[i++] = item;
        return table;
    }

    /// <summary>
    /// Checks cache for a table or creates a new one if one doesn't exist
    /// </summary>
    /// <returns>True if value was obtained from cache</returns>
    private bool GetCachedOrNewTable((Type, int) key, out LuaTable value)
    {
        if (_tableCache.TryGetValue(key, out value!)) return true;
        value = GetNewTable();
        _tableCache[key] = value;
        return false;
    }
}
