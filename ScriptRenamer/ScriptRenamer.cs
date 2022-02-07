using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLua;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using NLua.Exceptions;

namespace ScriptRenamer
{
    [Renamer(RenamerId)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ScriptRenamer : IRenamer
    {
        private const string RenamerId = nameof(ScriptRenamer);
        private static readonly Type Repofact = GetTypeFromAssemblies("Shoko.Server.Repositories.RepoFactory");
        private static readonly dynamic VideoLocalRepo = Repofact?.GetProperty("VideoLocal")?.GetValue(null);
        private static readonly dynamic ImportFolderRepo = Repofact?.GetProperty("ImportFolder")?.GetValue(null);
        private static readonly NLuaSingleton Lua = new();

        private static string _scriptCache;
        private static readonly Dictionary<string, (DateTime setTIme, string filename, IImportFolder destination, string subfolder)> ResultCache = new();

        private static (string filename, IImportFolder destination, string subfolder)? CheckCache(MoveEventArgs args)
        {
            var crc = args.FileInfo.Hashes.CRC;
            if (args.Script.Script != _scriptCache)
            {
                _scriptCache = args.Script.Script;
                ResultCache.Clear();
                return null;
            }
            if (!ResultCache.TryGetValue(crc, out var res)) return null;
            ResultCache.Remove(crc);
            if (res.setTIme < DateTime.UtcNow + TimeSpan.FromSeconds(2))
                return (res.filename, res.destination, res.subfolder);
            return null;
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            CheckBadArgs(args);
            var result = GetInfo(args);
            return (result?.destination, result?.subfolder);
        }

        public string GetFilename(RenameEventArgs args)
        {
            var mvEventArgs = new MoveEventArgs
            {
                Cancel = args.Cancel,
                AvailableFolders = ((IEnumerable)ImportFolderRepo.GetAll()).Cast<IImportFolder>()
                    .Where(a => a.DropFolderType != DropFolderType.Excluded).ToList(),
                FileInfo = args.FileInfo,
                AnimeInfo = args.AnimeInfo,
                GroupInfo = args.GroupInfo,
                EpisodeInfo = args.EpisodeInfo,
                Script = args.Script
            };
            CheckBadArgs(mvEventArgs);
            var result = GetInfo(mvEventArgs);
            return result?.filename;
        }

        private static (IImportFolder destination, string subfolder)? GetExistingAnimeLocation(MoveEventArgs args)
        {
            IImportFolder oldFld = null;
            var lastFileLocation = (IVideoFile)args.AnimeInfo.SelectMany(anime => (IEnumerable<dynamic>)VideoLocalRepo.GetByAniDBAnimeID(anime.AnimeID))
                .Where(vl => !string.Equals(vl.CRC32, args.FileInfo.Hashes.CRC, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(vl => vl.DateTimeUpdated)
                .Select(vl => vl.GetBestVideoLocalPlace())
                .FirstOrDefault(vlp => (oldFld = (IImportFolder)ImportFolderRepo.GetByID(vlp.ImportFolderID)) is not null &&
                                       (oldFld.DropFolderType.HasFlag(DropFolderType.Destination) ||
                                        oldFld.DropFolderType.HasFlag(DropFolderType.Excluded)));
            if (oldFld is null || lastFileLocation is null) return null;
            var oldLoc = NormPath(oldFld.Location);
            var subFld = Path.GetRelativePath(oldLoc, Path.GetDirectoryName(lastFileLocation.FilePath)!);
            return (oldFld, subFld);
        }

        public static (string filename, IImportFolder destination, string subfolder)? GetInfo(MoveEventArgs args)
        {
            var res = CheckCache(args);
            if (res is not null)
                return res;
            var result = Lua.RunSandboxed(args.Script.Script, CreateLuaEnv(args));
            var env = Lua.Inst.GetTableDict(result.env);
            var removeReservedChars = (bool)env[LuaEnv.RemoveReservedChars];
            var useExistingAnimeLocation = (bool)env[LuaEnv.UseExistingAnimeLocation];
            if (env.TryGetValue(LuaEnv.Filename, out var luaFilename) && luaFilename is not (string or null))
                throw new LuaScriptException("filename must be a string", string.Empty);
            var filename = !string.IsNullOrWhiteSpace((string)luaFilename)
                ? RemoveInvalidFilenameChars(removeReservedChars ? (string)luaFilename : ((string)luaFilename).ReplaceInvalidPathCharacters()) +
                  Path.GetExtension(args.FileInfo.Filename)
                : args.FileInfo.Filename;
            if (env.TryGetValue(LuaEnv.Destination, out var luaDestination) && luaDestination is not (string or LuaTable or null))
                throw new LuaScriptException("destination must be an import folder name, an import folder, or an array of path segments", string.Empty);
            IImportFolder destination;
            if (env.TryGetValue(LuaEnv.Subfolder, out var luaSubfolder) && luaSubfolder is not (LuaTable or null))
                throw new LuaScriptException("subfolder must be an array of path segments", string.Empty);
            string subfolder;
            (IImportFolder, string)? existingAnimeLocation = null;
            if (useExistingAnimeLocation) existingAnimeLocation = GetExistingAnimeLocation(args);
            if (existingAnimeLocation is null)
                (destination, subfolder) = (GetNewDestination(args, luaDestination), GetNewSubfolder(args, luaSubfolder, removeReservedChars));
            else
                (destination, subfolder) = existingAnimeLocation.Value;
            if (filename is null || destination is null || subfolder is null) return null;
            ResultCache.Add(args.FileInfo.Hashes.CRC, (DateTime.UtcNow, filename, destination, subfolder));
            return (filename, destination, subfolder);
        }

        private static Dictionary<string, object> CreateLuaEnv(MoveEventArgs args)
        {
            List<Dictionary<string, object>> ConvertTitles(IEnumerable<AnimeTitle> titles)
            {
                return titles.Select(t => new Dictionary<string, object>
                {
                    { "title", t.Title },
                    { "language", Convert.ToInt32(t.Language) },
                    { "languagecode", t.LanguageCode },
                    { "type", Convert.ToInt32(t.Type) }
                }).ToList();
            }

            var anime = args.AnimeInfo.Select(a => new Dictionary<string, object>
            {
                { "airdate", a.AirDate?.ToTable() },
                { "enddate", a.EndDate?.ToTable() },
                { "rating", a.Rating },
                { "restricted", a.Restricted },
                { "type", Convert.ToInt32(a.Type) },
                { "preferredtitle", a.PreferredTitle },
                { "animeid", a.AnimeID },
                {
                    "titles", ConvertTitles(a.Titles)
                },
                {
                    "episodecounts", new Dictionary<int, object>
                    {
                        { Convert.ToInt32(EpisodeType.Episode), a.EpisodeCounts.Episodes },
                        { Convert.ToInt32(EpisodeType.Special), a.EpisodeCounts.Specials },
                        { Convert.ToInt32(EpisodeType.Credits), a.EpisodeCounts.Credits },
                        { Convert.ToInt32(EpisodeType.Trailer), a.EpisodeCounts.Trailers },
                        { Convert.ToInt32(EpisodeType.Other), a.EpisodeCounts.Others },
                        { Convert.ToInt32(EpisodeType.Parody), a.EpisodeCounts.Parodies }
                    }
                }
            }).ToList();
            var anidb = args.FileInfo.AniDBFileInfo is null
                ? null
                : new Dictionary<string, object>
                {
                    { "censored", args.FileInfo.AniDBFileInfo.Censored },
                    { "source", args.FileInfo.AniDBFileInfo.Source },
                    { "version", args.FileInfo.AniDBFileInfo.Version },
                    { "releasedate", args.FileInfo.AniDBFileInfo.ReleaseDate?.ToTable() },
                    {
                        "releasegroup", args.FileInfo.AniDBFileInfo.ReleaseGroup is null
                            ? null
                            : new Dictionary<string, object>
                            {
                                { "name", args.FileInfo.AniDBFileInfo.ReleaseGroup.Name },
                                { "shortname", args.FileInfo.AniDBFileInfo.ReleaseGroup.ShortName }
                            }
                    },
                    { "fileid", args.FileInfo.AniDBFileInfo.AniDBFileID },
                    {
                        "media", new Dictionary<string, object>
                        {
                            { "videocodec", args.FileInfo.AniDBFileInfo.MediaInfo.VideoCodec },
                            {
                                "sublanguages",
                                args.FileInfo.AniDBFileInfo.MediaInfo.SubLanguages.Select(a => Convert.ToInt32(a)).ToList()
                            },
                            {
                                "dublanguages",
                                args.FileInfo.AniDBFileInfo.MediaInfo.AudioLanguages.Select(a => Convert.ToInt32(a)).ToList()
                            }
                        }
                    }
                };
            var mediainfo = args.FileInfo.MediaInfo is null
                ? null
                : new Dictionary<string, object>
                {
                    { "chaptered", args.FileInfo.MediaInfo.Chaptered },
                    {
                        "video", new Dictionary<string, object>
                        {
                            { "height", args.FileInfo.MediaInfo.Video.Height },
                            { "width", args.FileInfo.MediaInfo.Video.Width },
                            { "codec", args.FileInfo.MediaInfo.Video.SimplifiedCodec },
                            { "res", args.FileInfo.MediaInfo.Video.StandardizedResolution },
                            { "bitrate", args.FileInfo.MediaInfo.Video.BitRate },
                            { "bitdepth", args.FileInfo.MediaInfo.Video.BitDepth },
                            { "framerate", args.FileInfo.MediaInfo.Video.FrameRate }
                        }
                    },
                    { "duration", args.FileInfo.MediaInfo.General.Duration },
                    { "bitrate", args.FileInfo.MediaInfo.General.OverallBitRate },
                    {
                        "sublanguages", args.FileInfo.MediaInfo.Subs.Select(s =>
                            ParseEnum<TitleLanguage>(s.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                ? ParseEnum<TitleLanguage>(s.Title, false)
                                : l).Select(a => Convert.ToInt32(a)).ToList()
                    },
                    {
                        "audio", args.FileInfo.MediaInfo.Audio.Select(a => new Dictionary<string, object>
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
                                "language", Convert.ChangeType(
                                    ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown
                                        ? ParseEnum<TitleLanguage>(a.Title, false)
                                        : l, TypeCode.Int32)
                            },
                            { "title", a.Title }
                        }).ToList()
                    }
                };
            var file = new Dictionary<string, object>
            {
                { "name", args.FileInfo.Filename },
                { "path", args.FileInfo.FilePath },
                { "size", args.FileInfo.FileSize },
                {
                    "hashes", new Dictionary<string, object>
                    {
                        { "crc", args.FileInfo.Hashes.CRC },
                        { "md5", args.FileInfo.Hashes.MD5 },
                        { "ed2k", args.FileInfo.Hashes.ED2K },
                        { "sha1", args.FileInfo.Hashes.SHA1 },
                    }
                },
                { "anidb", anidb },
                { "media", mediainfo }
            };
            var episodes = args.EpisodeInfo.Select(e => new Dictionary<string, object>
            {
                { "duration", e.Duration },
                { "number", e.Number },
                { "type", Convert.ToInt32(e.Type) },
                { "airdate", e.AirDate?.ToTable() },
                { "animeid", e.AnimeID },
                { "episodeid", e.EpisodeID },
                { "titles", ConvertTitles(e.Titles) }
            }).ToList();
            var importfolders = args.AvailableFolders.Select(f => new Dictionary<string, object>
            {
                { "name", f.Name },
                { "location", f.Location },
                { "type", Convert.ToInt32(f.DropFolderType) }
            }).ToList();
            var groups = args.GroupInfo.Select(g => new Dictionary<string, object>
            {
                { "name", g.Name },
                // Just give Ids, subject to change if there is ever a reason to use more.
                { "mainSeriesId", g.MainSeries?.AnimeID },
                { "seriesIds", g.Series.Select(s => s.AnimeID).ToList() }
            }).ToList();
            return new Dictionary<string, object>
            {
                { LuaEnv.Filename, "" },
                //{ LuaEnv.Destination, "" },
                //{ LuaEnv.Subfolder, new Dictionary<string, object>() },
                { LuaEnv.RemoveReservedChars, false },
                { LuaEnv.UseExistingAnimeLocation, false },
                { LuaEnv.Animes, anime },
                { LuaEnv.File, file },
                { LuaEnv.Episodes, episodes },
                { LuaEnv.ImportFolders, importfolders },
                { LuaEnv.Groups, groups }
            };
        }

        private static Type GetTypeFromAssemblies(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(currentassembly => currentassembly.GetType(typeName, false, true))
                .FirstOrDefault(t => t is not null);
        }

        private static string GetNewSubfolder(MoveEventArgs args, object subfolder, bool removeReservedChars)
        {
            List<string> newSubFolderSplit;
            switch (subfolder)
            {
                case null:
                    newSubFolderSplit = new List<string> { args.AnimeInfo.OrderBy(a => a.AnimeID).First().PreferredTitle };
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
            newSubFolderSplit = newSubFolderSplit.Select(f => RemoveInvalidFilenameChars(removeReservedChars ? f : f.ReplaceInvalidPathCharacters()))
                .ToList();
            var newSubfolder = NormPath(string.Join(Path.DirectorySeparatorChar, newSubFolderSplit));
            return newSubfolder;
        }

        private static IImportFolder GetNewDestination(MoveEventArgs args, object destination)
        {
            IImportFolder destfolder = null;
            if (destination is string d && string.IsNullOrWhiteSpace(d))
                destination = null;
            switch (destination)
            {
                case null:
                    destfolder = args.AvailableFolders
                        // Order by common prefix (stronger version of same drive)
                        .OrderByDescending(f => string.Concat(NormPath(args.FileInfo.FilePath)
                            .TakeWhile((ch, i) => i < NormPath(f.Location).Length
                                                  && char.ToUpperInvariant(NormPath(f.Location)[i]) == char.ToUpperInvariant(ch))).Length)
                        .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
                    break;
                case string s:
                    destfolder = args.AvailableFolders.FirstOrDefault(f =>
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
                            destfolder = args.AvailableFolders.FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination)
                                                                                   && string.Equals(NormPath(f.Location), NormPath((string)kvp.Value),
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
                    var newDest = NormPath(string.Join(Path.DirectorySeparatorChar, newDestSplit));
                    destfolder = args.AvailableFolders.FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination)
                                                                           && string.Equals(NormPath(f.Location), NormPath(newDest),
                                                                               StringComparison.OrdinalIgnoreCase));
                    if (destfolder is null)
                        throw new ArgumentException($"Could not find destination folder by path: {newDest}");
                    break;
                default:
                    throw new ArgumentException("destination was not an expected type");
            }
            return destfolder;
        }


        private static void CheckBadArgs(MoveEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Script?.Script))
                throw new ArgumentException("Script is empty or null");
            if (args.Script.Type != RenamerId)
                throw new ArgumentException($"Script doesn't match {RenamerId}");
            if (args.AnimeInfo.Count == 0 || args.EpisodeInfo.Count == 0)
                throw new ArgumentException("No anime and/or episode info");
        }

        private static string NormPath(string path)
        {
            return path?.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        }

        private static string RemoveInvalidFilenameChars(string filename)
        {
            filename = filename.RemoveInvalidPathCharacters();
            filename = string.Concat(filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            return filename;
        }

        private static T ParseEnum<T>(string text, bool throwException = true)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), string.Concat(text.Where(c => !char.IsWhiteSpace(c))), true);
            }
            catch
            {
                if (throwException)
                    throw;
                return default;
            }
        }
    }
}
