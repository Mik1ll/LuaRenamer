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
    public class ScriptRenamer : IRenamer
    {
        private const string RenamerId = nameof(ScriptRenamer);
        private static readonly Type Repofact = GetTypeFromAssemblies("Shoko.Server.Repositories.RepoFactory");
        private static readonly dynamic VideoLocalRepo = Repofact.GetProperty("VideoLocal")?.GetValue(null);
        private static readonly dynamic ImportFolderRepo = Repofact.GetProperty("ImportFolder")?.GetValue(null);
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
            if (CheckBadArgs(args))
            {
                args.Cancel = true;
                return (null, null);
            }
            var result = GetInfo(args);
            return (result?.destination, result?.subfolder);
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
            if (CheckBadArgs(mvEventArgs))
            {
                args.Cancel = true;
                return null;
            }
            var result = GetInfo(mvEventArgs);
            return result?.filename;
        }

        private static (string filename, IImportFolder destination, string subfolder)? GetInfo(MoveEventArgs args)
        {
            var res = CheckCache(args);
            if (res is not null)
                return res;
            Lua.RunSandboxed(args.Script.Script);
            if (Lua.Inst["filename"] is not (string or null))
                throw new LuaScriptException("filename must be a string", string.Empty);
            var filename = (string)Lua.Inst["filename"];
            var luaDestination = Lua.Inst["destination"];
            if (luaDestination is not (string or IImportFolder or LuaTable or null))
                throw new LuaScriptException("destination must be an import folder name, an import folder, or an array of path segments", string.Empty);
            if (Lua.Inst["subfolder"] is not (string or LuaTable or null))
                throw new LuaScriptException("subfolder must be a string or an array of path segments", string.Empty);
            var luaSubfolder = (LuaTable)Lua.Inst["subfolder"];
            string subfolder;
            IImportFolder destination;
            var removeReservedChars = (bool)Lua.Inst["remove_reserved_chars"];
            var useExistingAnimeLocation = (bool)Lua.Inst["use_existing_anime_location"];

            filename = !string.IsNullOrWhiteSpace(filename)
                ? RemoveInvalidFilenameChars(removeReservedChars ? filename : filename.ReplaceInvalidPathCharacters()) +
                  Path.GetExtension(args.FileInfo.Filename)
                : null;
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
                case string s:
                    newSubFolderSplit = new List<string> { s };
                    break;
                case LuaTable subfolderTable:
                {
                    var subfolderDict = new SortedDictionary<long, string>();
                    foreach (KeyValuePair<object, object> kvp in subfolderTable)
                    {
                        if (kvp.Key is not long key)
                            throw new LuaScriptException("subfolder can't be a non-array table, found non-integer index", string.Empty);
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
            newSubFolderSplit = newSubFolderSplit.Select(f => RemoveInvalidFilenameChars(removeReservedChars ? f : f.ReplaceInvalidPathCharacters())).ToList();
            var newSubfolder = NormPath(newSubFolderSplit.Aggregate((current, t) => current + (t + Path.DirectorySeparatorChar)));
            return newSubfolder;
        }

        private static IImportFolder GetNewDestination(MoveEventArgs args, object destination)
        {
            IImportFolder destfolder;
            if (destination is string d && string.IsNullOrWhiteSpace(d))
                destination = null;
            switch (destination)
            {
                case null:
                    destfolder = args.AvailableFolders
                        // Order by common prefix (stronger version of same drive)
                        .OrderBy(f => string.Concat(NormPath(args.FileInfo.FilePath)
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
                        throw new ArgumentException($"Could not find destination folder by name (NOTE: You must use an array of path segments if using a path): {s}");
                    break;
                case IImportFolder imp:
                    destfolder = imp;
                    break;
                case LuaTable destinationTable:
                    var destDict = new SortedDictionary<long, string>();
                    foreach (KeyValuePair<object, object> kvp in destinationTable)
                    {
                        if (kvp.Key is not long key)
                            throw new LuaScriptException("destination can't be a non-array table, found non-integer index", string.Empty);
                        if (kvp.Value is not string val)
                            throw new LuaScriptException("destination array must only contain strings", string.Empty);
                        destDict[key] = val;
                    }
                    var newDestSplit = destDict.Values.ToList();
                    var newDest = NormPath(newDestSplit.Aggregate((current, t) => current + (t + Path.DirectorySeparatorChar)));
                    destfolder = args.AvailableFolders.FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination)
                                                                           && string.Equals(NormPath(f.Location), newDest, StringComparison.OrdinalIgnoreCase));
                    if (destfolder is null)
                        throw new ArgumentException($"Could not find destination folder by path: {newDest}");
                    break;
                default:
                    throw new ArgumentException("destination was not an expected type");
            }
            return destfolder;
        }


        private static bool CheckBadArgs(MoveEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Script?.Script))
                throw new ArgumentException("Script is empty or null");
            if (args.Script.Type != RenamerId)
                throw new ArgumentException($"Script doesn't match {RenamerId}");
            return args.AnimeInfo is null || args.EpisodeInfo is null;
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
    }
}
