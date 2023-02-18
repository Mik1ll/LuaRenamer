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

    private Dictionary<string, object?> CreateLuaEnv()
    {
        List<Dictionary<string, string?>> ConvertTitles(IEnumerable<AnimeTitle> titles)
        {
            return titles.Select(t => new Dictionary<string, string?>
            {
                { LuaEnv.title.name, t.Title },
                { LuaEnv.title.language, t.Language.ToString() },
                { LuaEnv.title.languagecode, t.LanguageCode },
                { LuaEnv.title.type, t.Type.ToString() }
            }).ToList();
        }

        Dictionary<string, string> ConvertEnum<T>() =>
            Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a!.ToString()!, a => a!.ToString()!);

        Dictionary<int, Dictionary<string, object?>> animeCache = new();

        Dictionary<string, object?> AnimeToDict(IAnime a, bool ignoreRelations = false)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (animeCache.TryGetValue(a.AnimeID, out var val)) return val;
            return animeCache[a.AnimeID] = new Dictionary<string, object?>
            {
                { LuaEnv.anime.airdate, a.AirDate?.ToTable() },
                { LuaEnv.anime.enddate, a.EndDate?.ToTable() },
                { LuaEnv.anime.rating, a.Rating },
                { LuaEnv.anime.restricted, a.Restricted },
                { LuaEnv.anime.type, a.Type.ToString() },
                { LuaEnv.anime.preferredname, a.PreferredTitle },
                { LuaEnv.anime.id, a.AnimeID },
                { LuaEnv.anime.titles, ConvertTitles(a.Titles) },
                { LuaEnv.anime.getname, _functions.GetName },
                { LuaEnv.anime._classid, LuaEnv.anime._classidVal },
                {
                    LuaEnv.anime.episodecounts, new Dictionary<string, int>
                    {
                        { EpisodeType.Episode.ToString(), a.EpisodeCounts.Episodes },
                        { EpisodeType.Special.ToString(), a.EpisodeCounts.Specials },
                        { EpisodeType.Credits.ToString(), a.EpisodeCounts.Credits },
                        { EpisodeType.Trailer.ToString(), a.EpisodeCounts.Trailers },
                        { EpisodeType.Other.ToString(), a.EpisodeCounts.Others },
                        { EpisodeType.Parody.ToString(), a.EpisodeCounts.Parodies }
                    }
                },
                {
                    LuaEnv.anime.relations.N, ignoreRelations
                        ? new List<Dictionary<string, object?>>()
                        : a.Relations.Where(r => r.RelatedAnime is not null && r.RelatedAnime.AnimeID != a.AnimeID)
                            .Select(r => new Dictionary<string, object?>
                            {
                                { LuaEnv.anime.relations.type, r.RelationType.ToString() },
                                { LuaEnv.anime.relations.anime, AnimeToDict(r.RelatedAnime, true) }
                            }).ToList()
                }
            };
        }

        var animes = _renamer.AnimeInfo.Select(a => AnimeToDict(a)).ToList();
        var anidb = _renamer.FileInfo.AniDBFileInfo is null
            ? null
            : new Dictionary<string, object?>
            {
                { LuaEnv.file.anidb.censored, _renamer.FileInfo.AniDBFileInfo.Censored },
                { LuaEnv.file.anidb.source, _renamer.FileInfo.AniDBFileInfo.Source },
                { LuaEnv.file.anidb.version, _renamer.FileInfo.AniDBFileInfo.Version },
                { LuaEnv.file.anidb.releasedate, _renamer.FileInfo.AniDBFileInfo.ReleaseDate?.ToTable() },
                {
                    LuaEnv.file.anidb.releasegroup.N, _renamer.FileInfo.AniDBFileInfo.ReleaseGroup is null
                                                      || _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.Name == "raw/unknown"
                        ? null
                        : new Dictionary<string, object>
                        {
                            { LuaEnv.file.anidb.releasegroup.name, _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.Name },
                            { LuaEnv.file.anidb.releasegroup.shortname, _renamer.FileInfo.AniDBFileInfo.ReleaseGroup.ShortName }
                        }
                },
                { LuaEnv.file.anidb.id, _renamer.FileInfo.AniDBFileInfo.AniDBFileID },
                {
                    LuaEnv.file.anidb.media.N, new Dictionary<string, object>
                    {
                        {
                            LuaEnv.file.anidb.media.sublanguages,
                            _renamer.FileInfo.AniDBFileInfo.MediaInfo.SubLanguages.Select(l => l.ToString()).ToList()
                        },
                        {
                            LuaEnv.file.anidb.media.dublanguages,
                            _renamer.FileInfo.AniDBFileInfo.MediaInfo.AudioLanguages.Select(l => l.ToString()).ToList()
                        }
                    }
                },
                { LuaEnv.file.anidb.description, _renamer.FileInfo.AniDBFileInfo.Description }
            };
        var mediainfo = _renamer.FileInfo.MediaInfo is null
            ? null
            : new Dictionary<string, object>
            {
                { LuaEnv.file.media.chaptered, _renamer.FileInfo.MediaInfo.Chaptered },
                {
                    LuaEnv.file.media.video.N, new Dictionary<string, object>
                    {
                        { LuaEnv.file.media.video.height, _renamer.FileInfo.MediaInfo.Video.Height },
                        { LuaEnv.file.media.video.width, _renamer.FileInfo.MediaInfo.Video.Width },
                        { LuaEnv.file.media.video.codec, _renamer.FileInfo.MediaInfo.Video.SimplifiedCodec },
                        { LuaEnv.file.media.video.res, _renamer.FileInfo.MediaInfo.Video.StandardizedResolution },
                        { LuaEnv.file.media.video.bitrate, _renamer.FileInfo.MediaInfo.Video.BitRate },
                        { LuaEnv.file.media.video.bitdepth, _renamer.FileInfo.MediaInfo.Video.BitDepth },
                        { LuaEnv.file.media.video.framerate, _renamer.FileInfo.MediaInfo.Video.FrameRate }
                    }
                },
                { LuaEnv.file.media.duration, _renamer.FileInfo.MediaInfo.General.Duration },
                { LuaEnv.file.media.bitrate, _renamer.FileInfo.MediaInfo.General.OverallBitRate },
                {
                    LuaEnv.file.media.sublanguages, _renamer.FileInfo.MediaInfo.Subs.Select(s =>
                        (Utils.ParseEnum<TitleLanguage>(s.LanguageName, false) is var l && l is TitleLanguage.Unknown
                            ? Utils.ParseEnum<TitleLanguage>(s.Title, false)
                            : l).ToString()).ToList()
                },
                {
                    LuaEnv.file.media.audio.N, _renamer.FileInfo.MediaInfo.Audio.Select(a => new Dictionary<string, object>
                    {
                        { LuaEnv.file.media.audio.compressionmode, a.Compression_Mode },
                        {
                            LuaEnv.file.media.audio.channels,
                            ((string?)((dynamic)a).ChannelLayout)?.Contains("LFE") ?? false ? a.Channels - 1 + 0.1 : a.Channels
                        },
                        { LuaEnv.file.media.audio.samplingrate, a.SamplingRate },
                        { LuaEnv.file.media.audio.codec, ((dynamic)a).Format },
                        {
                            LuaEnv.file.media.audio.language, (Utils.ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                ? Utils.ParseEnum<TitleLanguage>(a.Title, false)
                                : l).ToString()
                        },
                        { LuaEnv.file.media.audio.title, a.Title }
                    }).ToList()
                }
            };
        var importfolders = _renamer.AvailableFolders.Select((f, i) => new Dictionary<string, object>
        {
            { LuaEnv.importfolder.name, f.Name },
            { LuaEnv.importfolder.location, f.Location },
            { LuaEnv.importfolder.type, f.DropFolderType.ToString() },
            { LuaEnv.importfolder._classid, LuaEnv.importfolder._classidVal },
            { LuaEnv.importfolder._index, i }
        }).ToList();
        var file = new Dictionary<string, object?>
        {
            { LuaEnv.file.name, _renamer.FileInfo.Filename },
            { LuaEnv.file.path, _renamer.FileInfo.FilePath },
            { LuaEnv.file.size, _renamer.FileInfo.FileSize },
            {
                LuaEnv.file.hashes.N, new Dictionary<string, object>
                {
                    { LuaEnv.file.hashes.crc, _renamer.FileInfo.Hashes.CRC },
                    { LuaEnv.file.hashes.md5, _renamer.FileInfo.Hashes.MD5 },
                    { LuaEnv.file.hashes.ed2k, _renamer.FileInfo.Hashes.ED2K },
                    { LuaEnv.file.hashes.sha1, _renamer.FileInfo.Hashes.SHA1 },
                }
            },
            { LuaEnv.file.anidb.N, anidb },
            { LuaEnv.file.media.N, mediainfo },
            {
                LuaEnv.file.importfolder,
                importfolders.First(i => _renamer.FileInfo.FilePath.NormPath().StartsWith(((string)i[LuaEnv.importfolder.location]).NormPath()))
            }
        };
        var episodes = _renamer.EpisodeInfo.Select(e => new Dictionary<string, object?>
        {
            { LuaEnv.episode.duration, e.Duration },
            { LuaEnv.episode.number, e.Number },
            { LuaEnv.episode.type, e.Type.ToString() },
            { LuaEnv.episode.airdate, e.AirDate?.ToTable() },
            { LuaEnv.episode.animeid, e.AnimeID },
            { LuaEnv.episode.id, e.EpisodeID },
            { LuaEnv.episode.titles, ConvertTitles(e.Titles) },
            { LuaEnv.episode.getname, _functions.GetName },
            { LuaEnv.episode.prefix, Utils.EpPrefix[e.Type] },
            { LuaEnv.episode._classid, LuaEnv.episode._classidVal }
        }).ToList();
        var groups = _renamer.GroupInfo.Select(g => new Dictionary<string, object?>
        {
            { LuaEnv.group.name, g.Name },
            { LuaEnv.group.mainanime, g.MainSeries is null ? null : AnimeToDict(g.MainSeries) },
            { LuaEnv.group.animes, g.Series.Select(a => AnimeToDict(a)) }
        }).ToList();
        return new Dictionary<string, object?>
        {
            { LuaEnv.filename, null },
            { LuaEnv.destination, null },
            { LuaEnv.subfolder, null },
            { LuaEnv.replace_illegal_chars, false },
            { LuaEnv.remove_illegal_chars, false },
            { LuaEnv.use_existing_anime_location, false },
            { LuaEnv.animes, animes },
            { LuaEnv.anime.N, animes.First() },
            { LuaEnv.file.N, file },
            { LuaEnv.episodes, episodes },
            {
                LuaEnv.episode.N, episodes.Where(e => (int)e[LuaEnv.episode.animeid]! == (int)animes.First()[LuaEnv.anime.id]!)
                    .OrderBy(e => (string)e[LuaEnv.episode.type]! == EpisodeType.Other.ToString()
                        ? int.MinValue
                        : (int)Enum.Parse<EpisodeType>((string)e[LuaEnv.episode.type]!))
                    .ThenBy(e => (int)e[LuaEnv.episode.number]!)
                    .First()
            },
            { LuaEnv.importfolders, importfolders },
            { LuaEnv.groups, groups },
            { LuaEnv.group.N, groups.FirstOrDefault() },
            { LuaEnv.episode_numbers, _functions.EpNums },
            { LuaEnv.log, _functions.Log },
            { LuaEnv.logwarn, _functions.LogWarn },
            { LuaEnv.logerror, _functions.LogError },

            { LuaEnv.AnimeType, ConvertEnum<AnimeType>() },
            { LuaEnv.TitleType, ConvertEnum<TitleType>() },
            { LuaEnv.Language, ConvertEnum<TitleLanguage>() },
            { LuaEnv.EpisodeType, ConvertEnum<EpisodeType>() },
            { LuaEnv.ImportFolderType, ConvertEnum<DropFolderType>() },
            { LuaEnv.RelationType, ConvertEnum<RelationType>() }
        };
    }
}
