using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer;

public class LuaContext : Lua
{
    private readonly ILogger _logger;
    private readonly LuaRenamer _renamer;
    private readonly LuaFunctions _functions;
    public static readonly string LuaPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua");

    #region Sandbox

    private const string BaseEnv = @"
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
";

    private const string LuaLinqEnv = @"
from = from,
fromArray = fromArray,
fromArrayInstance = fromArrayInstance,
fromDictionary = fromDictionary,
fromIterator = fromIterator,
fromIteratorsArray = fromIteratorsArray,
fromSet = fromSet,
fromNothing = fromNothing,
";

    private const string SandboxFunction = @"
return function (untrusted_code, env)
  local untrusted_function, message = load(untrusted_code, nil, 't', env)
  if not untrusted_function then return nil, message end
  return untrusted_function()
end
";

    #endregion

    #region Lua Function Bindings

    private record LuaFunctions(LuaFunction RunSandbox, LuaFunction GetName, LuaFunction Log, LuaFunction LogWarn, LuaFunction LogError,
        LuaFunction EpNums);

    #region Logger Binding

    private void Log(string message) => _logger.LogInformation(message);
    private static readonly MethodInfo LogMethod = typeof(LuaContext).GetMethod(nameof(Log), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private void LogWarn(string message) => _logger.LogWarning(message);
    private static readonly MethodInfo LogWarnMethod = typeof(LuaContext).GetMethod(nameof(LogWarn), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private void LogError(string message) => _logger.LogError(message);
    private static readonly MethodInfo LogErrorMethod = typeof(LuaContext).GetMethod(nameof(LogError), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private string? GetName(LuaTable anime_or_episode, string language, bool allow_unofficial = false)
    {
        var titles = (string)anime_or_episode[LuaEnv.anime._classid] switch
        {
            LuaEnv.anime._classidVal => _renamer.AnimeInfo.First(a => (long)anime_or_episode[LuaEnv.anime.id] == a.AnimeID).Titles,
            LuaEnv.episode._classidVal => _renamer.EpisodeInfo.First(e => (long)anime_or_episode[LuaEnv.episode.id] == e.EpisodeID).Titles,
            _ => throw new ArgumentException("Self is not recognized as an Anime or Episode (class id nil or mismatch)")
        };
        var lang = Enum.Parse<TitleLanguage>(language);
        var title = titles
            .OrderBy(t => t.Type == TitleType.None ? int.MaxValue : (int)t.Type)
            .Where(t => t.Language == lang && (t.Type is TitleType.Main or TitleType.Official or TitleType.None ||
                                               allow_unofficial && t.Type is TitleType.Synonym)).Select(t => t.Title).FirstOrDefault();
        return title;
    }

    private static readonly MethodInfo GetNameMethod =
        typeof(LuaContext).GetMethod(nameof(GetName), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private string EpNums(int pad) => _renamer.EpisodeInfo.Where(e => e.AnimeID == _renamer.AnimeInfo.First().AnimeID) // All episodes with same anime id
        .OrderBy(e => e.Number)
        .GroupBy(e => e.Type)
        .OrderBy(g => g.Key)
        .Aggregate("", (epString, epTypeGroup) =>
            epString + " " + epTypeGroup.Aggregate( // Combine substring for each episode type
                (InRange: false, LastNum: -1, EpSubstring: ""),
                (tuple, ep) => (ep.Number == tuple.LastNum + 1, ep.Number,
                        tuple.EpSubstring +
                        (ep.Number != tuple.LastNum + 1 // Append to string if not in a range
                            ? (tuple.InRange ? "-" + tuple.LastNum.ToString($"d{pad}") : "") + // If a range ended, append the last number
                              " " + Utils.EpPrefix[epTypeGroup.Key] + ep.Number.ToString($"d{pad}") // Add current prefix and number
                            : "")
                    ),
                tuple => tuple.EpSubstring + (tuple.InRange ? "-" + tuple.LastNum.ToString($"d{pad}") : "") // If a range ended, append the last number
            ).Trim()
        ).Trim();

    private static readonly MethodInfo EpNumsMethod =
        typeof(LuaContext).GetMethod(nameof(EpNums), BindingFlags.Instance | BindingFlags.NonPublic)!;

    #endregion

    public LuaContext(ILogger logger, LuaRenamer renamer)
    {
        _logger = logger;
        _renamer = renamer;
        State.Encoding = Encoding.UTF8;
        DoFile(Path.Combine(LuaPath, "utils.lua"));
        DoFile(Path.Combine(LuaPath, "lualinq.lua"));
        _functions = new LuaFunctions(
            (LuaFunction)DoString(SandboxFunction)[0],
            RegisterFunction(LuaEnv.anime.getname, this, GetNameMethod),
            RegisterFunction(LuaEnv.log, this, LogMethod),
            RegisterFunction(LuaEnv.logwarn, this, LogWarnMethod),
            RegisterFunction(LuaEnv.logerror, this, LogErrorMethod),
            RegisterFunction(LuaEnv.episode_numbers, this, EpNumsMethod)
        );
    }

    public Dictionary<object, object> RunSandboxed()
    {
        var env = CreateLuaEnv();
        var luaEnv = (LuaTable)DoString($"r = {{{BaseEnv}{LuaLinqEnv}}}; r._G = r; setmetatable(string, {{ __index = r.string}}); return r")[0];
        foreach (var (k, v) in env) this.AddObject(luaEnv, v, k);
        var retVal = _functions.RunSandbox.Call(_renamer.Script.Script, luaEnv);
        if (retVal.Length == 2 && retVal[0] == null && retVal[1] is string errStr)
            throw new ArgumentException(errStr);
        return GetTableDict(luaEnv);
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> CreateLuaEnv()
    {
        Dictionary<int, Dictionary<string, object?>> animeCache = new();

        var animes = _renamer.AnimeInfo.Select(a => AnimeToDict(a, animeCache)).ToList();
        var anidb = AniDbFileToDict();
        var mediainfo = MediaInfoToDict();
        var importfolders = ImportFoldersToDict();
        var file = FileToDict(anidb, mediainfo, importfolders);
        var episodes = EpisodesToDict();
        var groups = GroupsToDict(animeCache);

        var env = new Dictionary<string, object?>();
        env.Add(LuaEnv.filename, null);
        env.Add(LuaEnv.destination, null);
        env.Add(LuaEnv.subfolder, null);
        env.Add(LuaEnv.replace_illegal_chars, false);
        env.Add(LuaEnv.remove_illegal_chars, false);
        env.Add(LuaEnv.use_existing_anime_location, false);
        env.Add(LuaEnv.skip_rename, false);
        env.Add(LuaEnv.skip_move, false);
        env.Add(LuaEnv.animes, animes);
        env.Add(LuaEnv.anime.N, animes.First());
        env.Add(LuaEnv.file.N, file);
        env.Add(LuaEnv.episodes, episodes);
        env.Add(LuaEnv.episode.N, episodes.Where(e => (int)e[LuaEnv.episode.animeid]! == (int)animes.First()[LuaEnv.anime.id]!)
            .OrderBy(e => (string)e[LuaEnv.episode.type]! == EpisodeType.Other.ToString()
                ? int.MinValue
                : (int)Enum.Parse<EpisodeType>((string)e[LuaEnv.episode.type]!))
            .ThenBy(e => (int)e[LuaEnv.episode.number]!)
            .First());
        env.Add(LuaEnv.importfolders, importfolders);
        env.Add(LuaEnv.groups, groups);
        env.Add(LuaEnv.group.N, groups.FirstOrDefault());
        env.Add(LuaEnv.episode_numbers, _functions.EpNums);
        env.Add(LuaEnv.log, _functions.Log);
        env.Add(LuaEnv.logwarn, _functions.LogWarn);
        env.Add(LuaEnv.logerror, _functions.LogError);
        env.Add(LuaEnv.AnimeType, EnumToDict<AnimeType>());
        env.Add(LuaEnv.TitleType, EnumToDict<TitleType>());
        env.Add(LuaEnv.Language, EnumToDict<TitleLanguage>());
        env.Add(LuaEnv.EpisodeType, EnumToDict<EpisodeType>());
        env.Add(LuaEnv.ImportFolderType, EnumToDict<DropFolderType>());
        env.Add(LuaEnv.RelationType, EnumToDict<RelationType>());
        return env;
    }

    private Dictionary<string, string> EnumToDict<T>() => Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a!.ToString()!, a => a!.ToString()!);

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, object?>> GroupsToDict(Dictionary<int, Dictionary<string, object?>> animeCache)
    {
        var groups = _renamer.GroupInfo.Select(g =>
        {
            var groupdict = new Dictionary<string, object?>();
            groupdict.Add(LuaEnv.group.name, g.Name);
            groupdict.Add(LuaEnv.group.mainanime, g.MainSeries is null ? null : AnimeToDict(g.MainSeries, animeCache));
            groupdict.Add(LuaEnv.group.animes, g.Series.Select(a => AnimeToDict(a, animeCache)).ToList());
            return groupdict;
        }).ToList();
        return groups;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> AnimeToDict(IAnime anime, Dictionary<int, Dictionary<string, object?>> animeCache, bool ignoreRelations = false)
    {
        if (anime == null) throw new ArgumentNullException(nameof(anime));
        if (animeCache.TryGetValue(anime.AnimeID, out var animedict)) return animedict;
        animedict = new Dictionary<string, object?>();
        animedict.Add(LuaEnv.anime.airdate, anime.AirDate?.ToTable());
        animedict.Add(LuaEnv.anime.enddate, anime.EndDate?.ToTable());
        animedict.Add(LuaEnv.anime.rating, anime.Rating);
        animedict.Add(LuaEnv.anime.restricted, anime.Restricted);
        animedict.Add(LuaEnv.anime.type, anime.Type.ToString());
        animedict.Add(LuaEnv.anime.preferredname, anime.PreferredTitle);
        animedict.Add(LuaEnv.anime.id, anime.AnimeID);
        animedict.Add(LuaEnv.anime.titles, ConvertTitles(anime.Titles));
        animedict.Add(LuaEnv.anime.getname, _functions.GetName);
        animedict.Add(LuaEnv.anime._classid, LuaEnv.anime._classidVal);
        var epcountdict = new Dictionary<string, int>();
        epcountdict.Add(EpisodeType.Episode.ToString(), anime.EpisodeCounts.Episodes);
        epcountdict.Add(EpisodeType.Special.ToString(), anime.EpisodeCounts.Specials);
        epcountdict.Add(EpisodeType.Credits.ToString(), anime.EpisodeCounts.Credits);
        epcountdict.Add(EpisodeType.Trailer.ToString(), anime.EpisodeCounts.Trailers);
        epcountdict.Add(EpisodeType.Other.ToString(), anime.EpisodeCounts.Others);
        epcountdict.Add(EpisodeType.Parody.ToString(), anime.EpisodeCounts.Parodies);
        animedict.Add(LuaEnv.anime.episodecounts, epcountdict);
        animedict.Add(LuaEnv.anime.relations.N, ignoreRelations
            ? new List<Dictionary<string, object?>>()
            : anime.Relations.Where(r => r.RelatedAnime is not null && r.RelatedAnime.AnimeID != anime.AnimeID)
                .Select(r =>
                {
                    var relationdict = new Dictionary<string, object?>();
                    relationdict.Add(LuaEnv.anime.relations.type, r.RelationType.ToString());
                    relationdict.Add(LuaEnv.anime.relations.anime, AnimeToDict(r.RelatedAnime, animeCache, true));
                    return relationdict;
                }).ToList());
        return animeCache[anime.AnimeID] = animedict;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? AniDbFileToDict()
    {
        Dictionary<string, object?>? anidb = null;
        if (_renamer.FileInfo.AniDBFileInfo is not null)
        {
            anidb = new Dictionary<string, object?>();
            anidb.Add(LuaEnv.file.anidb.censored, _renamer.FileInfo.AniDBFileInfo.Censored);
            anidb.Add(LuaEnv.file.anidb.source, _renamer.FileInfo.AniDBFileInfo.Source);
            anidb.Add(LuaEnv.file.anidb.version, _renamer.FileInfo.AniDBFileInfo.Version);
            anidb.Add(LuaEnv.file.anidb.releasedate, _renamer.FileInfo.AniDBFileInfo.ReleaseDate?.ToTable());
            Dictionary<string, object>? groupdict = null;
            if (_renamer.FileInfo.AniDBFileInfo.ReleaseGroup is not null && _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.Name != "raw/unknown")
            {
                groupdict = new Dictionary<string, object>();
                groupdict.Add(LuaEnv.file.anidb.releasegroup.name, _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.Name);
                groupdict.Add(LuaEnv.file.anidb.releasegroup.shortname, _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.ShortName);
            }
            anidb.Add(LuaEnv.file.anidb.releasegroup.N, groupdict);
            anidb.Add(LuaEnv.file.anidb.id, _renamer.FileInfo.AniDBFileInfo.AniDBFileID);
            var mediadict = new Dictionary<string, object>();
            mediadict.Add(LuaEnv.file.anidb.media.sublanguages, _renamer.FileInfo.AniDBFileInfo.MediaInfo.SubLanguages.Select(l => l.ToString()).ToList());
            mediadict.Add(LuaEnv.file.anidb.media.dublanguages, _renamer.FileInfo.AniDBFileInfo.MediaInfo.AudioLanguages.Select(l => l.ToString()).ToList());
            anidb.Add(LuaEnv.file.anidb.media.N, mediadict);
            anidb.Add(LuaEnv.file.anidb.description, _renamer.FileInfo.AniDBFileInfo.Description);
        }
        return anidb;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, object?>> EpisodesToDict()
    {
        var episodes = _renamer.EpisodeInfo.Select(e =>
        {
            var epdict = new Dictionary<string, object?>();
            epdict.Add(LuaEnv.episode.duration, e.Duration);
            epdict.Add(LuaEnv.episode.number, e.Number);
            epdict.Add(LuaEnv.episode.type, e.Type.ToString());
            epdict.Add(LuaEnv.episode.airdate, e.AirDate?.ToTable());
            epdict.Add(LuaEnv.episode.animeid, e.AnimeID);
            epdict.Add(LuaEnv.episode.id, e.EpisodeID);
            epdict.Add(LuaEnv.episode.titles, ConvertTitles(e.Titles));
            epdict.Add(LuaEnv.episode.getname, _functions.GetName);
            epdict.Add(LuaEnv.episode.prefix, Utils.EpPrefix[e.Type]);
            epdict.Add(LuaEnv.episode._classid, LuaEnv.episode._classidVal);
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
            title.Add(LuaEnv.title.name, t.Title);
            title.Add(LuaEnv.title.language, t.Language.ToString());
            title.Add(LuaEnv.title.languagecode, t.LanguageCode);
            title.Add(LuaEnv.title.type, t.Type.ToString());
            return title;
        }).ToList();
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?> FileToDict(Dictionary<string, object?>? anidb, Dictionary<string, object?>? mediainfo,
        List<Dictionary<string, object>> importfolders)
    {
        var file = new Dictionary<string, object?>();
        file.Add(LuaEnv.file.name, _renamer.FileInfo.Filename);
        file.Add(LuaEnv.file.path, _renamer.FileInfo.FilePath);
        file.Add(LuaEnv.file.size, _renamer.FileInfo.FileSize);
        var hashdict = new Dictionary<string, object?>();
        hashdict.Add(LuaEnv.file.hashes.crc, _renamer.FileInfo.Hashes.CRC);
        hashdict.Add(LuaEnv.file.hashes.md5, _renamer.FileInfo.Hashes.MD5);
        hashdict.Add(LuaEnv.file.hashes.ed2k, _renamer.FileInfo.Hashes.ED2K);
        hashdict.Add(LuaEnv.file.hashes.sha1, _renamer.FileInfo.Hashes.SHA1);
        file.Add(LuaEnv.file.hashes.N, hashdict);
        file.Add(LuaEnv.file.anidb.N, anidb);
        file.Add(LuaEnv.file.media.N, mediainfo);
        file.Add(LuaEnv.file.importfolder,
            importfolders.First(i => _renamer.FileInfo.FilePath.NormPath().StartsWith(((string)i[LuaEnv.importfolder.location]).NormPath())));
        return file;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private List<Dictionary<string, object>> ImportFoldersToDict()
    {
        var importfolders = _renamer.AvailableFolders.Select((f, i) =>
        {
            var importdict = new Dictionary<string, object>();
            importdict.Add(LuaEnv.importfolder.name, f.Name);
            importdict.Add(LuaEnv.importfolder.location, f.Location);
            importdict.Add(LuaEnv.importfolder.type, f.DropFolderType.ToString());
            importdict.Add(LuaEnv.importfolder._classid, LuaEnv.importfolder._classidVal);
            importdict.Add(LuaEnv.importfolder._index, i);
            return importdict;
        }).ToList();
        return importfolders;
    }

    [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
    private Dictionary<string, object?>? MediaInfoToDict()
    {
        Dictionary<string, object?>? mediainfo = null;
        if (_renamer.FileInfo.MediaInfo is not null)
        {
            mediainfo = new Dictionary<string, object?>();
            mediainfo.Add(LuaEnv.file.media.chaptered, _renamer.FileInfo.MediaInfo.Chaptered);
            Dictionary<string, object>? videodict = null;
            if (_renamer.FileInfo.MediaInfo.Video is not null)
            {
                videodict = new Dictionary<string, object>();
                videodict.Add(LuaEnv.file.media.video.height, _renamer.FileInfo.MediaInfo.Video.Height);
                videodict.Add(LuaEnv.file.media.video.width, _renamer.FileInfo.MediaInfo.Video.Width);
                videodict.Add(LuaEnv.file.media.video.codec, _renamer.FileInfo.MediaInfo.Video.SimplifiedCodec);
                videodict.Add(LuaEnv.file.media.video.res, _renamer.FileInfo.MediaInfo.Video.StandardizedResolution);
                videodict.Add(LuaEnv.file.media.video.bitrate, _renamer.FileInfo.MediaInfo.Video.BitRate);
                videodict.Add(LuaEnv.file.media.video.bitdepth, _renamer.FileInfo.MediaInfo.Video.BitDepth);
                videodict.Add(LuaEnv.file.media.video.framerate, _renamer.FileInfo.MediaInfo.Video.FrameRate);
            }
            mediainfo.Add(LuaEnv.file.media.video.N, videodict);
            mediainfo.Add(LuaEnv.file.media.duration, _renamer.FileInfo.MediaInfo.General.Duration);
            mediainfo.Add(LuaEnv.file.media.bitrate, _renamer.FileInfo.MediaInfo.General.OverallBitRate);
            mediainfo.Add(LuaEnv.file.media.sublanguages, _renamer.FileInfo.MediaInfo.Subs.Select(s =>
                (Utils.ParseEnum<TitleLanguage>(s.LanguageName, false) is var l && l is TitleLanguage.Unknown
                    ? Utils.ParseEnum<TitleLanguage>(s.Title, false)
                    : l).ToString()).ToList());
            mediainfo.Add(LuaEnv.file.media.audio.N, _renamer.FileInfo.MediaInfo.Audio.Select(a =>
            {
                var audiodict = new Dictionary<string, object>();
                audiodict.Add(LuaEnv.file.media.audio.compressionmode, a.Compression_Mode);
                audiodict.Add(LuaEnv.file.media.audio.channels,
                    ((string?)((dynamic)a).ChannelLayout)?.Contains("LFE") ?? false ? a.Channels - 1 + 0.1 : a.Channels);
                audiodict.Add(LuaEnv.file.media.audio.samplingrate, a.SamplingRate);
                audiodict.Add(LuaEnv.file.media.audio.codec, ((dynamic)a).Format);
                audiodict.Add(LuaEnv.file.media.audio.language, (Utils.ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown
                    ? Utils.ParseEnum<TitleLanguage>(a.Title, false)
                    : l).ToString());
                audiodict.Add(LuaEnv.file.media.audio.title, a.Title);
                return audiodict;
            }).ToList());
        }
        return mediainfo;
    }
}
