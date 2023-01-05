using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NLua;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer
{
    public class LuaContext : Lua
    {
        private readonly ILogger _logger;
        private readonly MoveEventArgs _args;
        private readonly LuaFunctions _functions;
        private static readonly string LuaLinqLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lua", "lualinq.lua");

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

        private record LuaFunctions(LuaFunction RunSandbox, LuaFunction Title, LuaFunction Log, LuaFunction LogWarn, LuaFunction LogError, LuaFunction EpNums);

        #region Logger Binding

        private void Log(string message) => _logger.LogInformation(message);
        private static readonly MethodInfo LogMethod = typeof(LuaContext).GetMethod(nameof(Log), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private void LogWarn(string message) => _logger.LogWarning(message);
        private static readonly MethodInfo LogWarnMethod = typeof(LuaContext).GetMethod(nameof(LogWarn), BindingFlags.Instance | BindingFlags.NonPublic)!;
        private void LogError(string message) => _logger.LogError(message);
        private static readonly MethodInfo LogErrorMethod = typeof(LuaContext).GetMethod(nameof(LogError), BindingFlags.Instance | BindingFlags.NonPublic)!;

        #endregion

        private static readonly string TitleFunction = $@"
return function (self, language, allow_unofficial)
  local titles = from(self.{LuaEnv.anime.titles}):where(function (a) return a.{LuaEnv.title.language} == language; end)
                                  :orderby(function (a) return ({{ {nameof(TitleType.Main)} = 0, {nameof(TitleType.Official)} = 1, {nameof(TitleType.Synonym)} = 2, {nameof(TitleType.Short)} = 3, {nameof(TitleType.None)} = 4 }})[a.{LuaEnv.title.type}] end)
  local title = allow_unofficial and titles:first() or titles:where(function (a) return ({{ {nameof(TitleType.Main)} = true, {nameof(TitleType.Official)} = true, {nameof(TitleType.None)} = true }})[a.{LuaEnv.title.type}] end):first()
  if title then return title.{LuaEnv.title.name} end
end
";

        private string EpNums(int pad) => _args.EpisodeInfo.Where(e => e.AnimeID == _args.AnimeInfo.First().AnimeID)
            .OrderBy(e => e.Number)
            .GroupBy(e => e.Type)
            .OrderBy(g => g.Key)
            .Aggregate("", (s, g) =>
                s + " " + g.Aggregate(
                    (InRun: false, Seq: -1, Str: ""),
                    (tup, ep) => ep.Number == tup.Seq + 1
                        ? (true, ep.Number, tup.Str)
                        : tup.InRun
                            ? (false, ep.Number,
                                $"{tup.Str}-{tup.Seq.ToString().PadLeft(pad, '0')} {Utils.EpPrefix[g.Key]}{ep.Number.ToString().PadLeft(pad, '0')}")
                            : (false, ep.Number, $"{tup.Str} {Utils.EpPrefix[g.Key]}{ep.Number.ToString().PadLeft(pad, '0')}"),
                    tup => tup.InRun ? $"{tup.Str}-{tup.Seq.ToString().PadLeft(pad, '0')}" : tup.Str
                ).Trim()
            ).Trim();

        private static readonly MethodInfo EpNumsMethod =
            typeof(LuaContext).GetMethod(nameof(EpNums), BindingFlags.Instance | BindingFlags.NonPublic)!;

        #endregion

        public LuaContext(ILogger logger, MoveEventArgs args)
        {
            _logger = logger;
            _args = args;
            State.Encoding = Encoding.UTF8;
            DoFile(LuaLinqLocation);
            _functions = new LuaFunctions(
                (LuaFunction)DoString(SandboxFunction)[0],
                (LuaFunction)DoString(TitleFunction)[0],
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
            var retVal = _functions.RunSandbox.Call(_args.Script.Script, luaEnv);
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

            var traversed = new List<int>{};
            Dictionary<string, object?> ConvertAnime(IAnime anime)
            {
                if (traversed.Contains(anime.AnimeID)) {
                    return new Dictionary<string, object?>{};
                }
                traversed.Add(anime.AnimeID);
                return new Dictionary<string, object?>
                {
                    { LuaEnv.anime.airdate, anime.AirDate?.ToTable() },
                    { LuaEnv.anime.enddate, anime.EndDate?.ToTable() },
                    { LuaEnv.anime.rating, anime.Rating },
                    { LuaEnv.anime.restricted, anime.Restricted },
                    { LuaEnv.anime.type, anime.Type.ToString() },
                    { LuaEnv.anime.preferredname, anime.PreferredTitle },
                    { LuaEnv.anime.id, anime.AnimeID },
                    { LuaEnv.anime.titles, ConvertTitles(anime.Titles) },
                    { LuaEnv.anime.getname, _functions.Title },
                    {
                        LuaEnv.anime.episodecounts, new Dictionary<string, int>
                        {
                            { EpisodeType.Episode.ToString(), anime.EpisodeCounts.Episodes },
                            { EpisodeType.Special.ToString(), anime.EpisodeCounts.Specials },
                            { EpisodeType.Credits.ToString(), anime.EpisodeCounts.Credits },
                            { EpisodeType.Trailer.ToString(), anime.EpisodeCounts.Trailers },
                            { EpisodeType.Other.ToString(), anime.EpisodeCounts.Others },
                            { EpisodeType.Parody.ToString(), anime.EpisodeCounts.Parodies }
                        }
                    },
                    {
                        LuaEnv.anime.relations, anime.Relations.Select(a => new Dictionary<string, object?>
                        {
                            { LuaEnv.relatedanime.anime, ConvertAnime(a.RelatedAnime) },
                            { LuaEnv.relatedanime.relationtype, a.RelationType }
                        })
                    }
                };
            }

            var animes = _args.AnimeInfo.Select(a => ConvertAnime(a)).ToList();

            var anidb = _args.FileInfo.AniDBFileInfo is null
                ? null
                : new Dictionary<string, object?>
                {
                    { LuaEnv.file.anidb.censored, _args.FileInfo.AniDBFileInfo.Censored },
                    { LuaEnv.file.anidb.source, _args.FileInfo.AniDBFileInfo.Source },
                    { LuaEnv.file.anidb.version, _args.FileInfo.AniDBFileInfo.Version },
                    { LuaEnv.file.anidb.releasedate, _args.FileInfo.AniDBFileInfo.ReleaseDate?.ToTable() },
                    {
                        LuaEnv.file.anidb.releasegroup.N, _args.FileInfo.AniDBFileInfo.ReleaseGroup is null
                                                          || _args.FileInfo.AniDBFileInfo.ReleaseGroup.Name == "raw/unknown"
                            ? null
                            : new Dictionary<string, object>
                            {
                                { LuaEnv.file.anidb.releasegroup.name, _args.FileInfo.AniDBFileInfo.ReleaseGroup.Name },
                                { LuaEnv.file.anidb.releasegroup.shortname, _args.FileInfo.AniDBFileInfo.ReleaseGroup.ShortName }
                            }
                    },
                    { LuaEnv.file.anidb.id, _args.FileInfo.AniDBFileInfo.AniDBFileID },
                    {
                        LuaEnv.file.anidb.media.N, new Dictionary<string, object>
                        {
                            {
                                LuaEnv.file.anidb.media.sublanguages,
                                _args.FileInfo.AniDBFileInfo.MediaInfo.SubLanguages.Select(l => l.ToString()).ToList()
                            },
                            {
                                LuaEnv.file.anidb.media.dublanguages,
                                _args.FileInfo.AniDBFileInfo.MediaInfo.AudioLanguages.Select(l => l.ToString()).ToList()
                            }
                        }
                    },
                    { LuaEnv.file.anidb.description, _args.FileInfo.AniDBFileInfo.Description }
                };
            var mediainfo = _args.FileInfo.MediaInfo is null
                ? null
                : new Dictionary<string, object>
                {
                    { LuaEnv.file.media.chaptered, _args.FileInfo.MediaInfo.Chaptered },
                    {
                        LuaEnv.file.media.video.N, new Dictionary<string, object>
                        {
                            { LuaEnv.file.media.video.height, _args.FileInfo.MediaInfo.Video.Height },
                            { LuaEnv.file.media.video.width, _args.FileInfo.MediaInfo.Video.Width },
                            { LuaEnv.file.media.video.codec, _args.FileInfo.MediaInfo.Video.SimplifiedCodec },
                            { LuaEnv.file.media.video.res, _args.FileInfo.MediaInfo.Video.StandardizedResolution },
                            { LuaEnv.file.media.video.bitrate, _args.FileInfo.MediaInfo.Video.BitRate },
                            { LuaEnv.file.media.video.bitdepth, _args.FileInfo.MediaInfo.Video.BitDepth },
                            { LuaEnv.file.media.video.framerate, _args.FileInfo.MediaInfo.Video.FrameRate }
                        }
                    },
                    { LuaEnv.file.media.duration, _args.FileInfo.MediaInfo.General.Duration },
                    { LuaEnv.file.media.bitrate, _args.FileInfo.MediaInfo.General.OverallBitRate },
                    {
                        LuaEnv.file.media.sublanguages, _args.FileInfo.MediaInfo.Subs.Select(s =>
                            (Utils.ParseEnum<TitleLanguage>(s.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                ? Utils.ParseEnum<TitleLanguage>(s.Title, false)
                                : l).ToString()).ToList()
                    },
                    {
                        LuaEnv.file.media.audio.N, _args.FileInfo.MediaInfo.Audio.Select(a => new Dictionary<string, object>
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
            var importfolders = _args.AvailableFolders.Select((f, i) => new Dictionary<string, object>
            {
                { LuaEnv.importfolder.name, f.Name },
                { LuaEnv.importfolder.location, f.Location },
                { LuaEnv.importfolder.type, f.DropFolderType.ToString() },
                { LuaEnv.importfolder._classid, "55138454-4A0D-45EB-8CCE-1CCF00220165" },
                { LuaEnv.importfolder._index, i }
            }).ToList();
            var file = new Dictionary<string, object?>
            {
                { LuaEnv.file.name, _args.FileInfo.Filename },
                { LuaEnv.file.path, _args.FileInfo.FilePath },
                { LuaEnv.file.size, _args.FileInfo.FileSize },
                {
                    LuaEnv.file.hashes.N, new Dictionary<string, object>
                    {
                        { LuaEnv.file.hashes.crc, _args.FileInfo.Hashes.CRC },
                        { LuaEnv.file.hashes.md5, _args.FileInfo.Hashes.MD5 },
                        { LuaEnv.file.hashes.ed2k, _args.FileInfo.Hashes.ED2K },
                        { LuaEnv.file.hashes.sha1, _args.FileInfo.Hashes.SHA1 },
                    }
                },
                { LuaEnv.file.anidb.N, anidb },
                { LuaEnv.file.media.N, mediainfo },
                {
                    LuaEnv.file.importfolder,
                    importfolders.First(i => _args.FileInfo.FilePath.NormPath().StartsWith(((string)i[LuaEnv.importfolder.location]).NormPath()))
                }
            };
            var episodes = _args.EpisodeInfo.Select(e => new Dictionary<string, object?>
            {
                { LuaEnv.episode.duration, e.Duration },
                { LuaEnv.episode.number, e.Number },
                { LuaEnv.episode.type, e.Type.ToString() },
                { LuaEnv.episode.airdate, e.AirDate?.ToTable() },
                { LuaEnv.episode.animeid, e.AnimeID },
                { LuaEnv.episode.id, e.EpisodeID },
                { LuaEnv.episode.titles, ConvertTitles(e.Titles) },
                { LuaEnv.episode.getname, _functions.Title },
                { LuaEnv.episode.prefix, Utils.EpPrefix[e.Type] }
            }).ToList();
            var groups = _args.GroupInfo.Select(g => new Dictionary<string, object?>
            {
                { LuaEnv.group.name, g.Name },
                // Just give Ids, subject to change if there is ever a reason to use more.
                { LuaEnv.group.mainseriesid, g.MainSeries?.AnimeID },
                { LuaEnv.group.seriesids, g.Series.Select(s => s.AnimeID).ToList() }
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
            };
        }
    }
}
