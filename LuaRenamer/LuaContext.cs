using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.BaseTypes;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
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

    // The shared title-resolver closure for this env build; set by CreateLuaEnv before any *ToTable runs.
    private LuaFunction _getName = null!;


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
        _getName = (LuaFunction)runSandboxed.Call(GetNameFunction, env)[1];

        var animes = _args.Series
            .OrderBy(s => s.AnidbAnimeID != _primarySeries.AnidbAnimeID)
            .ThenBy(s => s.AnidbAnimeID)
            .Select(series => AnimeToTable(series.AnidbAnime, false)).ToList();
        var episodes = _args.Episodes
            .OrderBy(e => e.AnidbEpisodeID != _primaryEpisode.AnidbEpisodeID)
            .ThenBy(e => e.AnidbEpisode.SeriesID)
            .ThenBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.AnidbEpisode.Type)
            .ThenBy(e => e.AnidbEpisode.EpisodeNumber)
            .Select(e => EpisodeToTable(e.AnidbEpisode)).ToList();
        var groups = _args.Groups
            .OrderBy(g => g.MainSeriesID != _primarySeries.AnidbAnimeID)
            .Select(GroupToTable).ToList();

        _ = new EnvTable(env)
        {
            episode_numbers = (Func<long, string>)EpNums,
            logdebug = (Action<string>)LogDebug,
            log = (Action<string>)Log,
            logwarn = (Action<string>)LogWarn,
            logerror = (Action<string>)LogError,
            replace_illegal_chars = _args.Configuration.ReplaceIllegalCharacters,
            remove_illegal_chars = _args.Configuration.RemoveIllegalCharacters,
            use_existing_anime_location = _args.Configuration.UseExistingAnimeLocation,
            skip_rename = false,
            skip_move = false,
            illegal_chars_map = ReplaceMapToTable(),
            animes = ArrayOf(animes),
            anime = animes[0],
            file = FileToTable(_args.File),
            episodes = ArrayOf(episodes),
            episode = episodes[0],
            importfolders = ArrayOf(_args.AvailableFolders.Select(ImportFolderToTable)),
            groups = ArrayOf(groups),
            group = groups.Count > 0 ? groups[0] : null,
            tmdb = TmdbToTable(),
        };

        _ = new EnumsTable(env)
        {
            importFolderType = EnumToTable<DropFolderType>(),
            animeType = EnumToTable<AnimeType>(),
            episodeType = EnumToTable<EpisodeType>(),
            titleType = EnumToTable<TitleType>(),
            language = EnumToTable<TitleLanguage>(),
            relationType = EnumToTable<RelationType>(),
            seasonName = EnumToTable<YearlySeason>(),
        };

        return env;
    }

    // Use Distinct to prevent duplicate entries for enum values with the same underlying value
    private LuaEnumRef<T> EnumToTable<T>() where T : struct, Enum
    {
        var table = GetNewTable();
        foreach (var v in Enum.GetValues<T>().Distinct())
            table[Enum.GetName(v)!] = Enum.GetName(v);
        return new LuaEnumRef<T>(table);
    }

    private LuaMap<string, string> ReplaceMapToTable()
        => MapOf(FilePathCleaner.ReplaceMapDefaults.Select(kvp => (kvp.Key, kvp.Value)));

    private LuaRef<GroupTable> GroupToTable(IShokoGroup group) =>
        new GroupTable(GetNewTable())
        {
            name = string.IsNullOrWhiteSpace(group.PreferredTitle?.Value) ? null : group.PreferredTitle?.Value,
            mainanime = AnimeToTable(group.MainSeries.AnidbAnime, false),
            animes = ArrayOf(group.AllSeries.Select(a => AnimeToTable(a.AnidbAnime, false))),
        };

    private LuaRef<AnimeTable> AnimeToTable(IAnidbAnime anime, bool ignoreRelations)
    {
        ArgumentNullException.ThrowIfNull(anime);
        return Cached<IAnidbAnime, AnimeTable>(anime.ID, tbl =>
        {
            var series = anime.ShokoSeries.FirstOrDefault();
            return new AnimeTable(tbl)
            {
                getname = _getName,
                airdate = DateTimeToTable(anime.AirDate?.ToDateTime()),
                enddate = DateTimeToTable(anime.EndDate?.ToDateTime()),
                rating = anime.Rating,
                restricted = anime.Restricted,
                type = anime.Type,
                preferredname = string.IsNullOrWhiteSpace(series?.Title) ? anime.Title : series.Title,
                defaultname = string.IsNullOrWhiteSpace(series?.DefaultTitle.Value) ? anime.DefaultTitle.Value : series.DefaultTitle.Value,
                id = anime.ID,
                titles = ArrayOf(anime.Titles.OrderBy(t => t.Value).Select(TitleToTable)),
                studios = ArrayOf(anime.Studios.Select(st => st.Name)),
                episodecounts = MapOf(Enum.GetValues<EpisodeType>().Select(ep => (ep, (long)anime.EpisodeCounts[ep]))),
                relations = ArrayOf(ignoreRelations
                    ? Enumerable.Empty<LuaRef<RelationTable>>()
                    : anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID)
                        .Select(RelationToTable)),
                tags = ArrayOf(anime.Tags.Select(t => t.Name)),
                customtags = ArrayOf(series?.Tags.Select(t => t.Name) ?? []),
                seasons = ArrayOf(anime.YearlySeasons.Select(SeasonToTable)),
            };
        });
    }

    private LuaRef<SeasonTable> SeasonToTable((int Year, YearlySeason Season) season) =>
        new SeasonTable(GetNewTable())
        {
            year = season.Year,
            season = season.Season,
        };

    private LuaRef<RelationTable> RelationToTable(IRelatedMetadata<ISeries, ISeries> relation) =>
        new RelationTable(GetNewTable())
        {
            anime = AnimeToTable((relation.Related as IAnidbAnime)!, true),
            type = relation.RelationType,
        };

    private LuaRef<AniDbTable>? AniDbFileToTable(IReleaseInfo? aniDb)
    {
        if (aniDb is not { ReleaseURI: var releaseUri } || !(releaseUri?.StartsWith("https://anidb.net/file/") ?? false))
            return null;
        return new AniDbTable(GetNewTable())
        {
            id = int.Parse(aniDb.ReleaseURI![23..]),
            censored = aniDb.IsCensored,
            source = Enum.GetName(aniDb.Source)!,
            version = aniDb.Version,
            releasedate = DateTimeToTable(aniDb.ReleasedAt?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)),
            releasegroup = ReleaseGroupToTable(aniDb.Group),
            media = new AniDbMediaTable(GetNewTable())
            {
                sublanguages = ArrayOf(aniDb.MediaInfo?.SubtitleLanguages ?? []),
                dublanguages = ArrayOf(aniDb.MediaInfo?.AudioLanguages ?? []),
            },
            description = aniDb.Comment,
        };
    }

    private LuaRef<ReleaseGroupTable>? ReleaseGroupToTable(IReleaseGroup? releaseGroup)
    {
        if (releaseGroup?.ID is null || releaseGroup.Name == "raw/unknown")
            return null;
        return new ReleaseGroupTable(GetNewTable())
        {
            name = releaseGroup.Name,
            shortname = releaseGroup.ShortName,
        };
    }

    private LuaRef<EpisodeTable> EpisodeToTable(IAnidbEpisode episode) =>
        Cached<IAnidbEpisode, EpisodeTable>(episode.ID, tbl => new EpisodeTable(tbl)
        {
            getname = _getName,
            duration = (long)episode.Runtime.TotalSeconds,
            number = episode.EpisodeNumber,
            type = episode.Type,
            airdate = DateTimeToTable(episode.AirDateWithTime),
            animeid = episode.SeriesID,
            id = episode.ID,
            titles = ArrayOf(episode.Titles.OrderBy(t => t.Value).Select(TitleToTable)),
            prefix = Utils.EpPrefix[episode.Type],
        });

    private LuaRef<TitleTable> TitleToTable(ITitle title) =>
        new TitleTable(GetNewTable())
        {
            name = title.Value,
            language = title.Language,
            languagecode = title.LanguageCode,
            type = title.Type,
        };

    private LuaRef<FileTable> FileToTable(IVideoFile file) =>
        new FileTable(GetNewTable())
        {
            name = Path.GetFileNameWithoutExtension(file.FileName),
            extension = Path.GetExtension(file.FileName),
            path = file.Path,
            size = file.Size,
            earliestname = Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName),
            hashes = new HashesTable(GetNewTable())
            {
                crc = file.Video.Hashes.FirstOrDefault(h => h.Type is "CRC32")?.Value,
                md5 = file.Video.Hashes.FirstOrDefault(h => h.Type is "MD5")?.Value,
                ed2k = file.Video.ED2K,
                sha1 = file.Video.Hashes.FirstOrDefault(h => h.Type is "SHA1")?.Value,
            },
            anidb = AniDbFileToTable(file.Video.ReleaseInfo),
            media = MediaInfoToTable(file.Video.MediaInfo),
            importfolder = ImportFolderToTable(file.ManagedFolder),
        };

    private LuaRef<ImportFolderTable> ImportFolderToTable(IManagedFolder folder) =>
        Cached<IManagedFolder, ImportFolderTable>(folder.ID, tbl => new ImportFolderTable(tbl)
        {
            id = folder.ID,
            name = folder.Name,
            location = folder.Path,
            type = folder.DropFolderType,
        });

    private LuaRef<MediaTable>? MediaInfoToTable(IMediaInfo? mediaInfo)
    {
        if (mediaInfo is null)
            return null;
        return new MediaTable(GetNewTable())
        {
            video = mediaInfo.VideoStream is { } video ? VideoToTable(video) : null,
            chaptered = mediaInfo.Chapters.Any(),
            duration = (long)mediaInfo.Duration.TotalSeconds,
            bitrate = mediaInfo.BitRate,
            sublanguages = ArrayOf(mediaInfo.TextStreams.Select(s => s.Language.ToString())),
            audio = ArrayOf(mediaInfo.AudioStreams.Select(AudioToTable)),
        };
    }

    private LuaRef<VideoTable> VideoToTable(IVideoStream video) =>
        new VideoTable(GetNewTable())
        {
            height = video.Height,
            width = video.Width,
            codec = video.Codec.Simplified,
            res = video.Resolution,
            bitrate = video.BitRate,
            bitdepth = video.BitDepth,
            framerate = (double)video.FrameRate,
        };

    private LuaRef<AudioTable> AudioToTable(IAudioStream audio) =>
        new AudioTable(GetNewTable())
        {
            compressionmode = audio.CompressionMode,
            channels = !string.IsNullOrWhiteSpace(audio.ChannelLayout) && audio.ChannelLayout.Contains("LFE") ? audio.Channels - 1 + 0.1 : audio.Channels,
            samplingrate = audio.SamplingRate,
            codec = audio.Codec.Simplified,
            language = audio.Language.ToString(),
            title = audio.Title,
        };

    private LuaRef<DateTimeTable>? DateTimeToTable(DateTime? dateTime)
    {
        if (dateTime is not { } dt)
            return null;
        return new DateTimeTable(GetNewTable())
        {
            year = dt.Year,
            month = dt.Month,
            day = dt.Day,
            yday = dt.DayOfYear,
            wday = (long)dt.DayOfWeek + 1,
            hour = dt.Hour,
            min = dt.Minute,
            sec = dt.Second,
            isdst = dt.IsDaylightSavingTime(),
        };
    }

    private LuaRef<TmdbTable> TmdbToTable() =>
        new TmdbTable(GetNewTable())
        {
            movies = ArrayOf(_args.Series[0].TmdbMovies.Select(TmdbMovieToTable)),
            shows = ArrayOf(_args.Series[0].TmdbShows.Select(TmdbShowToTable)),
            episodes = ArrayOf(_args.Episodes.Where(e => e.SeriesID == _primarySeries.ID)
                .SelectMany(e => e.TmdbEpisodes.Select(TmdbEpisodeToTable))),
        };

    private LuaRef<TmdbMovieTable> TmdbMovieToTable(ITmdbMovie movie) =>
        new TmdbMovieTable(GetNewTable())
        {
            getname = _getName,
            id = movie.ID,
            titles = ArrayOf(movie.Titles.Select(TitleToTable)),
            defaultname = string.IsNullOrWhiteSpace(movie.DefaultTitle?.Value) ? null : movie.DefaultTitle?.Value,
            preferredname = string.IsNullOrWhiteSpace(movie.PreferredTitle?.Value) ? null : movie.PreferredTitle?.Value,
            rating = movie.Rating,
            restricted = movie.Restricted,
            studios = ArrayOf(movie.Studios.Select(s => s.Name)),
            airdate = DateTimeToTable(movie.ReleaseDate),
        };

    private LuaRef<TmdbShowTable> TmdbShowToTable(ITmdbShow show) =>
        new TmdbShowTable(GetNewTable())
        {
            getname = _getName,
            id = show.ID,
            titles = ArrayOf(show.Titles.Select(TitleToTable)),
            defaultname = string.IsNullOrWhiteSpace(show.DefaultTitle?.Value) ? null : show.DefaultTitle?.Value,
            preferredname = string.IsNullOrWhiteSpace(show.PreferredTitle?.Value) ? null : show.PreferredTitle?.Value,
            rating = show.Rating,
            restricted = show.Restricted,
            studios = ArrayOf(show.Studios.Select(st => st.Name)),
            episodecount = show.EpisodeCounts.Episodes,
            airdate = DateTimeToTable(show.AirDate?.ToDateTime()),
            enddate = DateTimeToTable(show.EndDate?.ToDateTime()),
            seasons = ArrayOf(show.YearlySeasons.Select(SeasonToTable)),
        };

    private LuaRef<TmdbEpisodeTable> TmdbEpisodeToTable(ITmdbEpisode episode) =>
        new TmdbEpisodeTable(GetNewTable())
        {
            getname = _getName,
            showid = episode.SeriesID,
            id = episode.ID,
            titles = ArrayOf(episode.Titles.Select(TitleToTable)),
            defaultname = string.IsNullOrWhiteSpace(episode.DefaultTitle?.Value) ? null : episode.DefaultTitle?.Value,
            preferredname = string.IsNullOrWhiteSpace(episode.PreferredTitle?.Value) ? null : episode.PreferredTitle?.Value,
            type = episode.Type,
            number = episode.EpisodeNumber,
            seasonnumber = episode.SeasonNumber,
            airdate = DateTimeToTable(episode.AirDateWithTime),
        };

    private LuaArray<LuaRef<T>> ArrayOf<T>(IEnumerable<LuaRef<T>> items) where T : Table
    {
        var table = GetNewTable();
        var i = 1;
        foreach (var item in items) table[i++] = item.Table;
        return new LuaArray<LuaRef<T>>(table);
    }

    private LuaArray<string> ArrayOf(IEnumerable<string> items)
    {
        var table = GetNewTable();
        var i = 1;
        foreach (var item in items) table[i++] = item;
        return new LuaArray<string>(table);
    }

    private LuaArray<T> ArrayOf<T>(IEnumerable<T> items) where T : struct, Enum
    {
        var table = GetNewTable();
        var i = 1;
        foreach (var item in items) table[i++] = item.ToString();
        return new LuaArray<T>(table);
    }

    private LuaMap<TKey, TVal> MapOf<TKey, TVal>(IEnumerable<(TKey Key, TVal Value)> entries)
    {
        var table = GetNewTable();
        foreach (var (k, v) in entries)
            table[k is Enum e ? e.ToString() : (object)k!] = v;
        return new LuaMap<TKey, TVal>(table);
    }

    private LuaTable GetNewTable()
    {
        NewTable("_");
        return GetTable("_");
    }

    /// <summary>
    /// Returns the cached handle for a (<typeparamref name="TKey"/>, <paramref name="id"/>) entity, or
    /// builds and caches a new one. The table is cached <em>before</em> <paramref name="build"/> runs so
    /// recursive references (e.g. Anime → relations → Anime) resolve to the same table.
    /// </summary>
    private LuaRef<TTable> Cached<TKey, TTable>(int id, Func<LuaTable, LuaRef<TTable>> build) where TTable : Table
    {
        var key = (typeof(TKey), id);
        if (_tableCache.TryGetValue(key, out var cached))
            return new LuaRef<TTable>(cached);
        var table = GetNewTable();
        _tableCache[key] = table;
        return build(table);
    }
}
