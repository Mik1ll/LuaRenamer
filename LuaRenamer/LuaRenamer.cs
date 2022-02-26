using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NLua;
using NLua.Exceptions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer
{
    [Renamer(LuaRenamer.RenamerId)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LuaRenamer : IRenamer
    {
        private const string RenamerId = nameof(LuaRenamer);
        private static readonly Type Repofact = Utils.GetTypeFromAssemblies("Shoko.Server.Repositories.RepoFactory");
        private static readonly dynamic VideoLocalRepo = Repofact?.GetProperty("VideoLocal")?.GetValue(null);
        private static readonly dynamic ImportFolderRepo = Repofact?.GetProperty("ImportFolder")?.GetValue(null);
        private static readonly NLuaSingleton Lua = new();

        private static string _scriptCache;
        private static readonly Dictionary<string, (DateTime setTIme, string filename, IImportFolder destination, string subfolder)> ResultCache = new();

        public MoveEventArgs Args;

        private (string filename, IImportFolder destination, string subfolder)? CheckCache()
        {
            var crc = Args.FileInfo.Hashes.CRC;
            if (Args.Script.Script != _scriptCache)
            {
                _scriptCache = Args.Script.Script;
                ResultCache.Clear();
                return null;
            }
            if (!ResultCache.TryGetValue(crc, out var res)) return null;
            ResultCache.Remove(crc);
            if (res.setTIme < DateTime.UtcNow + TimeSpan.FromSeconds(2))
                return (res.filename, res.destination, res.subfolder);
            return null;
        }

        public string GetFilename(RenameEventArgs args)
        {
            Args = RenameArgsToMoveArgs(args);
            CheckBadArgs();
            var result = GetInfo();
            return result?.filename;
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            Args = args;
            CheckBadArgs();
            var result = GetInfo();
            return (result?.destination, result?.subfolder);
        }

        private static MoveEventArgs RenameArgsToMoveArgs(RenameEventArgs args) => new()
        {
            Cancel = args.Cancel,
            AvailableFolders = ImportFolderRepo is not null
                ? ((IEnumerable)ImportFolderRepo.GetAll()).Cast<IImportFolder>()
                .Where(a => a.DropFolderType != DropFolderType.Excluded).ToList()
                : new List<IImportFolder>(),
            FileInfo = args.FileInfo,
            AnimeInfo = args.AnimeInfo,
            GroupInfo = args.GroupInfo,
            EpisodeInfo = args.EpisodeInfo,
            Script = args.Script
        };

        public (string filename, IImportFolder destination, string subfolder)? GetInfo()
        {
            var res = CheckCache();
            if (res is not null)
                return res;
            var result = RunSandboxed(Args.Script.Script);
            var env = Lua.Inst.GetTableDict(result.env);
            var removeReservedChars = (bool)env[LuaEnv.RemoveReservedChars];
            var useExistingAnimeLocation = (bool)env[LuaEnv.UseExistingAnimeLocation];
            if (env.TryGetValue(LuaEnv.Filename, out var luaFilename) && luaFilename is not (string or null))
                throw new LuaScriptException("filename must be a string", string.Empty);
            var filename = !string.IsNullOrWhiteSpace((string)luaFilename)
                ? Utils.RemoveInvalidFilenameChars(removeReservedChars ? (string)luaFilename : ((string)luaFilename).ReplaceInvalidPathCharacters()) +
                  Path.GetExtension(Args.FileInfo.Filename)
                : Args.FileInfo.Filename;
            if (env.TryGetValue(LuaEnv.Destination, out var luaDestination) && luaDestination is not (string or LuaTable or null))
                throw new LuaScriptException("destination must be an import folder name, an import folder, or an array of path segments", string.Empty);
            IImportFolder destination;
            if (env.TryGetValue(LuaEnv.Subfolder, out var luaSubfolder) && luaSubfolder is not (LuaTable or null))
                throw new LuaScriptException("subfolder must be an array of path segments", string.Empty);
            string subfolder;
            (IImportFolder, string)? existingAnimeLocation = null;
            if (useExistingAnimeLocation) existingAnimeLocation = GetExistingAnimeLocation();
            if (existingAnimeLocation is null)
                (destination, subfolder) = (GetNewDestination(luaDestination), GetNewSubfolder(luaSubfolder, removeReservedChars));
            else
                (destination, subfolder) = existingAnimeLocation.Value;
            if (filename is null || destination is null || subfolder is null) return null;
            ResultCache.Add(Args.FileInfo.Hashes.CRC, (DateTime.UtcNow, filename, destination, subfolder));
            return (filename, destination, subfolder);
        }

        private string GetNewSubfolder(object subfolder, bool removeReservedChars)
        {
            List<string> newSubFolderSplit;
            switch (subfolder)
            {
                case null:
                    newSubFolderSplit = new List<string> { Args.AnimeInfo.OrderBy(a => a.AnimeID).First().PreferredTitle };
                    break;
                case LuaTable subfolderTable:
                {
                    var subfolderDict = new SortedDictionary<long, string>();
                    foreach (KeyValuePair<object, object> kvp in subfolderTable)
                    {
                        if (kvp.Key is not long key)
                            continue;
                        if (kvp.Value is not string val)
                            throw new LuaScriptException("subfolder array must only contain strings", string.Empty);
                        subfolderDict[key] = val;
                    }
                    newSubFolderSplit = subfolderDict.Values.ToList();
                    break;
                }
                default:
                    throw new ArgumentException("subfolder was not an expected type");
            }
            newSubFolderSplit = newSubFolderSplit.Select(f => Utils.RemoveInvalidFilenameChars(removeReservedChars ? f : f.ReplaceInvalidPathCharacters()))
                .ToList();
            var newSubfolder = Utils.NormPath(string.Join(Path.DirectorySeparatorChar, newSubFolderSplit));
            return newSubfolder;
        }

        private IImportFolder GetNewDestination(object destination)
        {
            IImportFolder destfolder = null;
            if (destination is string d && string.IsNullOrWhiteSpace(d))
                destination = null;
            switch (destination)
            {
                case null:
                    destfolder = Args.AvailableFolders
                        // Order by common prefix (stronger version of same drive)
                        .OrderByDescending(f => string.Concat(Utils.NormPath(Args.FileInfo.FilePath)
                            .TakeWhile((ch, i) => i < Utils.NormPath(f.Location).Length
                                                  && char.ToUpperInvariant(Utils.NormPath(f.Location)[i]) == char.ToUpperInvariant(ch))).Length)
                        .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
                    break;
                case string s:
                    destfolder = Args.AvailableFolders.FirstOrDefault(f =>
                        f.DropFolderType.HasFlag(DropFolderType.Destination)
                        && string.Equals(f.Name, s, StringComparison.OrdinalIgnoreCase)
                    );
                    if (destfolder is null)
                        throw new ArgumentException(
                            $"Could not find destination folder by name (NOTE: You must use an array of path segments if using a path): {s}");
                    break;
                case LuaTable destinationTable:
                    foreach (KeyValuePair<object, object> kvp in destinationTable)
                    {
                        if ((string)kvp.Key == "location")
                        {
                            destfolder = Args.AvailableFolders.FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination)
                                                                                   && string.Equals(Utils.NormPath(f.Location),
                                                                                       Utils.NormPath((string)kvp.Value),
                                                                                       StringComparison.OrdinalIgnoreCase));
                            break;
                        }
                    }
                    if (destfolder is not null)
                        break;
                    var destDict = new SortedDictionary<long, string>();
                    foreach (KeyValuePair<object, object> kvp in destinationTable)
                    {
                        if (kvp.Key is not long key)
                            continue;
                        if (kvp.Value is not string val)
                            throw new LuaScriptException("destination array must only contain strings", string.Empty);
                        destDict[key] = val;
                    }
                    var newDestSplit = destDict.Values.ToList();
                    var newDest = Utils.NormPath(string.Join(Path.DirectorySeparatorChar, newDestSplit));
                    destfolder = Args.AvailableFolders.FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination)
                                                                           && string.Equals(Utils.NormPath(f.Location), Utils.NormPath(newDest),
                                                                               StringComparison.OrdinalIgnoreCase));
                    if (destfolder is null)
                        throw new ArgumentException($"Could not find destination folder by path: {newDest}");
                    break;
                default:
                    throw new ArgumentException("destination was not an expected type");
            }
            return destfolder;
        }

        private (IImportFolder destination, string subfolder)? GetExistingAnimeLocation()
        {
            IImportFolder oldFld = null;
            var lastFileLocation = ((IEnumerable<dynamic>)VideoLocalRepo.GetByAniDBAnimeID(Args.AnimeInfo[0].AnimeID))
                .Where(vl => !string.Equals(vl.CRC32, Args.FileInfo.Hashes.CRC, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(vl => vl.DateTimeUpdated)
                .Select(vl => vl.GetBestVideoLocalPlace())
                .FirstOrDefault(vlp => (oldFld = (IImportFolder)ImportFolderRepo.GetByID(vlp.ImportFolderID)) is not null &&
                                       (oldFld.DropFolderType.HasFlag(DropFolderType.Destination) ||
                                        oldFld.DropFolderType.HasFlag(DropFolderType.Excluded)));
            if (oldFld is null || lastFileLocation is null) return null;
            var subFld = Path.GetDirectoryName(lastFileLocation.FilePath);
            return (oldFld, subFld);
        }

        private void CheckBadArgs()
        {
            if (string.IsNullOrWhiteSpace(Args.Script?.Script))
                throw new ArgumentException("Script is empty or null");
            if (Args.Script.Type != RenamerId)
                throw new ArgumentException($"Script doesn't match {RenamerId}");
            if (Args.AnimeInfo.Count == 0 || Args.EpisodeInfo.Count == 0)
                throw new ArgumentException("No anime and/or episode info");
        }

        private (object[] retVal, LuaTable env) RunSandboxed(string code)
        {
            var env = CreateLuaEnv();
            var luaEnv = Lua.Inst.CreateEnv(Lua.BaseEnvStrings);
            Lua.Inst["env"] = luaEnv;
            var objCache = new Dictionary<object, LuaTable>();
            foreach (var (k, v) in env) Lua.Inst.AddObject(luaEnv, v, k, objCache);
            luaEnv[LuaEnv.EpisodeNumbers] = Lua.Inst.RegisterFunction(LuaEnv.EpisodeNumbers, this,
                GetType().GetMethod("GetEpisodesString", BindingFlags.NonPublic | BindingFlags.Instance));
            return (Lua.LuaRunSandboxed.Call(code, luaEnv), luaEnv);
        }

        private Dictionary<string, object> CreateLuaEnv()
        {
            List<Dictionary<string, object>> ConvertTitles(IEnumerable<AnimeTitle> titles)
            {
                return titles.Select(t => new Dictionary<string, object>
                {
                    { "title", t.Title },
                    { "language", t.Language },
                    { "languagecode", t.LanguageCode },
                    { "type", t.Type }
                }).ToList();
            }

            var animes = Args.AnimeInfo.Select(a => new Dictionary<string, object>
            {
                { "airdate", a.AirDate?.ToTable() },
                { "enddate", a.EndDate?.ToTable() },
                { "rating", a.Rating },
                { "restricted", a.Restricted },
                { "type", a.Type },
                { "preferredtitle", a.PreferredTitle },
                { "id", a.AnimeID },
                {
                    "titles", ConvertTitles(a.Titles)
                },
                {
                    "episodecounts", new Dictionary<EpisodeType, int>
                    {
                        { EpisodeType.Episode, a.EpisodeCounts.Episodes },
                        { EpisodeType.Special, a.EpisodeCounts.Specials },
                        { EpisodeType.Credits, a.EpisodeCounts.Credits },
                        { EpisodeType.Trailer, a.EpisodeCounts.Trailers },
                        { EpisodeType.Other, a.EpisodeCounts.Others },
                        { EpisodeType.Parody, a.EpisodeCounts.Parodies }
                    }
                }
            }).ToList();
            var anidb = Args.FileInfo.AniDBFileInfo is null
                ? null
                : new Dictionary<string, object>
                {
                    { "censored", Args.FileInfo.AniDBFileInfo.Censored },
                    { "source", Args.FileInfo.AniDBFileInfo.Source },
                    { "version", Args.FileInfo.AniDBFileInfo.Version },
                    { "releasedate", Args.FileInfo.AniDBFileInfo.ReleaseDate?.ToTable() },
                    {
                        "releasegroup", Args.FileInfo.AniDBFileInfo.ReleaseGroup is null
                            ? null
                            : new Dictionary<string, object>
                            {
                                { "name", Args.FileInfo.AniDBFileInfo.ReleaseGroup.Name },
                                { "shortname", Args.FileInfo.AniDBFileInfo.ReleaseGroup.ShortName }
                            }
                    },
                    { "id", Args.FileInfo.AniDBFileInfo.AniDBFileID },
                    {
                        "media", new Dictionary<string, object>
                        {
                            { "videocodec", Args.FileInfo.AniDBFileInfo.MediaInfo.VideoCodec },
                            {
                                "sublanguages",
                                Args.FileInfo.AniDBFileInfo.MediaInfo.SubLanguages.ToList()
                            },
                            {
                                "dublanguages",
                                Args.FileInfo.AniDBFileInfo.MediaInfo.AudioLanguages.ToList()
                            }
                        }
                    }
                };
            var mediainfo = Args.FileInfo.MediaInfo is null
                ? null
                : new Dictionary<string, object>
                {
                    { "chaptered", Args.FileInfo.MediaInfo.Chaptered },
                    {
                        "video", new Dictionary<string, object>
                        {
                            { "height", Args.FileInfo.MediaInfo.Video.Height },
                            { "width", Args.FileInfo.MediaInfo.Video.Width },
                            { "codec", Args.FileInfo.MediaInfo.Video.SimplifiedCodec },
                            { "res", Args.FileInfo.MediaInfo.Video.StandardizedResolution },
                            { "bitrate", Args.FileInfo.MediaInfo.Video.BitRate },
                            { "bitdepth", Args.FileInfo.MediaInfo.Video.BitDepth },
                            { "framerate", Args.FileInfo.MediaInfo.Video.FrameRate }
                        }
                    },
                    { "duration", Args.FileInfo.MediaInfo.General.Duration },
                    { "bitrate", Args.FileInfo.MediaInfo.General.OverallBitRate },
                    {
                        "sublanguages", Args.FileInfo.MediaInfo.Subs.Select(s =>
                            Utils.ParseEnum<TitleLanguage>(s.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                ? Utils.ParseEnum<TitleLanguage>(s.Title, false)
                                : l).ToList()
                    },
                    {
                        "audio", Args.FileInfo.MediaInfo.Audio.Select(a => new Dictionary<string, object>
                        {
                            { "compressionmode", a.Compression_Mode },
                            { "bitrate", a.BitRate },
                            { "channels", a.Channels },
                            { "bitdepth", a.BitDepth },
                            { "samplingrate", a.SamplingRate },
                            { "bitratemode", a.BitRate_Mode },
                            { "simplecodec", a.SimplifiedCodec },
                            { "codec", a.Codec },
                            {
                                "language", Utils.ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                    ? Utils.ParseEnum<TitleLanguage>(a.Title, false)
                                    : l
                            },
                            { "title", a.Title }
                        }).ToList()
                    }
                };
            var file = new Dictionary<string, object>
            {
                { "name", Args.FileInfo.Filename },
                { "path", Args.FileInfo.FilePath },
                { "size", Args.FileInfo.FileSize },
                {
                    "hashes", new Dictionary<string, object>
                    {
                        { "crc", Args.FileInfo.Hashes.CRC },
                        { "md5", Args.FileInfo.Hashes.MD5 },
                        { "ed2k", Args.FileInfo.Hashes.ED2K },
                        { "sha1", Args.FileInfo.Hashes.SHA1 },
                    }
                },
                { "anidb", anidb },
                { "media", mediainfo }
            };
            var episodes = Args.EpisodeInfo.Select(e => new Dictionary<string, object>
            {
                { "duration", e.Duration },
                { "number", e.Number },
                { "type", e.Type },
                { "airdate", e.AirDate?.ToTable() },
                { "animeid", e.AnimeID },
                { "id", e.EpisodeID },
                { "titles", ConvertTitles(e.Titles) },
                { "prefix", Utils.EpPrefix[e.Type] }
            }).ToList();
            var importfolders = Args.AvailableFolders.Select(f => new Dictionary<string, object>
            {
                { "name", f.Name },
                { "location", f.Location },
                { "type", f.DropFolderType }
            }).ToList();
            var groups = Args.GroupInfo.Select(g => new Dictionary<string, object>
            {
                { "name", g.Name },
                // Just give Ids, subject to change if there is ever a reason to use more.
                { "mainSeriesid", g.MainSeries?.AnimeID },
                { "seriesids", g.Series.Select(s => s.AnimeID).ToList() }
            }).ToList();
            return new Dictionary<string, object>
            {
                { LuaEnv.Filename, "" },
                //{ LuaEnv.Destination, "" },
                //{ LuaEnv.Subfolder, new Dictionary<string, object>() },
                { LuaEnv.RemoveReservedChars, false },
                { LuaEnv.UseExistingAnimeLocation, false },
                { LuaEnv.Animes, animes },
                { LuaEnv.Anime, animes[0] },
                { LuaEnv.File, file },
                { LuaEnv.Episodes, episodes },
                {
                    LuaEnv.Episode, episodes.Where(e => (int)e["animeid"] == (int)animes[0]["id"])
                        .OrderBy(e => (EpisodeType)e["type"] == EpisodeType.Other ? int.MinValue : (int)e["type"])
                        .ThenBy(e => (int)e["number"])
                        .First()
                },
                { LuaEnv.ImportFolders, importfolders },
                { LuaEnv.Groups, groups }
            };
        }

        // ReSharper disable once UnusedMember.Local
        private string GetEpisodesString(int pad) => Args.EpisodeInfo.Where(e => e.AnimeID == Args.AnimeInfo[0]?.AnimeID)
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
    }
}
