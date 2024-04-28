using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NLua;
using NLua.Exceptions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer;

[Renamer(RenamerId)]
// ReSharper disable once ClassNeverInstantiated.Global
public class LuaRenamer : IRenamer
{
    private readonly ILogger<LuaRenamer> _logger;
    public const string RenamerId = nameof(LuaRenamer);

    internal static string ScriptCache = string.Empty;
    internal static readonly Dictionary<int, (DateTime setTIme, string filename, IImportFolder destination, string subfolder)> ResultCache = new();

    private bool _skipRename;
    private bool _skipMove;

    public IVideoFile FileInfo { get; private set; } = null!;
    public IVideo VideoInfo { get; private set; } = null!;
    public IRenameScript Script { get; private set; } = null!;
    public IList<IGroup> GroupInfo { get; private set; } = null!;
    public IList<IEpisode> EpisodeInfo { get; private set; } = null!;
    public IList<IAnime> AnimeInfo { get; private set; } = null!;
    public List<IImportFolder> AvailableFolders { get; private set; } = null!;


    public LuaRenamer(ILogger<LuaRenamer> logger)
    {
        _logger = logger;
    }

    private (string filename, IImportFolder destination, string subfolder)? CheckCache()
    {
        var videoFileId = FileInfo.VideoID;
        if (Script.Script != ScriptCache)
        {
            ScriptCache = Script.Script;
            ResultCache.Clear();
            return null;
        }

        if (!ResultCache.TryGetValue(videoFileId, out var res))
            return null;
        if (DateTime.UtcNow < res.setTIme + TimeSpan.FromSeconds(2))
            return (res.filename, res.destination, res.subfolder);
        ResultCache.Remove(videoFileId);
        return null;
    }

    public string? GetFilename(RenameEventArgs args)
    {
        SetupArgs(args);
        try
        {
            CheckBadArgs();
            var result = GetInfo();
            return result?.filename;
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var frame = st.GetFrames().FirstOrDefault(f => f.GetFileName() is not null);
            return $"*Error: File: {frame?.GetFileName()} Method: {frame?.GetMethod()?.Name} Line: {frame?.GetFileLineNumber()} | {e.Message}";
        }
    }

    public (IImportFolder? destination, string? subfolder) GetDestination(MoveEventArgs args)
    {
        SetupArgs(args);
        try
        {
            CheckBadArgs();
            var result = GetInfo();
            return (result?.destination, result?.subfolder);
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var frame = st.GetFrames().FirstOrDefault(f => f.GetFileName() is not null);
            return (null, $"*Error: File: {frame?.GetFileName()} Method: {frame?.GetMethod()?.Name} Line: {frame?.GetFileLineNumber()} | {e.Message}");
        }
    }

    private void SetupArgs(RenameEventArgs args)
    {
        FileInfo = args.FileInfo;
        VideoInfo = args.VideoInfo;
        AnimeInfo = args.AnimeInfo.ToList();
        EpisodeInfo = args.EpisodeInfo.ToList();
        GroupInfo = args.GroupInfo.ToList();
        Script = args.Script;
        AvailableFolders = args.AvailableFolders.ToList();
    }


    public void SetupArgs(MoveEventArgs args)
    {
        FileInfo = args.FileInfo;
        VideoInfo = args.VideoInfo;
        AnimeInfo = args.AnimeInfo.ToList();
        EpisodeInfo = args.EpisodeInfo.ToList();
        GroupInfo = args.GroupInfo.ToList();
        Script = args.Script;
        AvailableFolders = args.AvailableFolders.ToList();
    }

    public (string filename, IImportFolder destination, string subfolder)? GetInfo()
    {
        if (CheckCache() is { } cacheHit)
        {
            _logger.LogInformation("Returning rename/move result from cache");
            return cacheHit;
        }

        using var lua = new LuaContext(_logger, this);
        var env = lua.RunSandboxed();
        var replaceIllegalChars = (bool)env[LuaEnv.replace_illegal_chars];
        var removeIllegalChars = (bool)env[LuaEnv.remove_illegal_chars];
        var useExistingAnimeLocation = (bool)env[LuaEnv.use_existing_anime_location];
        env.TryGetValue(LuaEnv.filename, out var luaFilename);
        env.TryGetValue(LuaEnv.destination, out var luaDestination);
        env.TryGetValue(LuaEnv.subfolder, out var luaSubfolder);
        env.TryGetValue(LuaEnv.skip_rename, out var luaSkipRename);
        _skipRename = (bool?)luaSkipRename ?? false;
        env.TryGetValue(LuaEnv.skip_move, out var luaSkipMove);
        _skipMove = (bool?)luaSkipMove ?? false;

        IImportFolder? destination;
        string? subfolder;
        string filename;
        if (_skipMove)
        {
            destination = FileInfo.ImportFolder;
            subfolder = SubfolderFromRelativePath(FileInfo);
        }
        else
            (destination, subfolder) = (useExistingAnimeLocation ? GetExistingAnimeLocation() : null) ??
                                       (GetNewDestination(luaDestination), GetNewSubfolder(luaSubfolder, replaceIllegalChars, removeIllegalChars));

        if (_skipRename)
            filename = FileInfo.FileName;
        else
            filename = luaFilename is string f
                ? (removeIllegalChars ? f : f.ReplacePathSegmentChars(replaceIllegalChars)).CleanPathSegment(true) + Path.GetExtension(FileInfo.FileName)
                : FileInfo.FileName;

        if (destination is null || string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(subfolder)) return null;
        ResultCache.Add(FileInfo.VideoID, (DateTime.UtcNow, filename, destination, subfolder));
        return (filename, destination, subfolder);
    }

    private string GetNewSubfolder(object? subfolder, bool replaceIllegalChars, bool removeIllegalChars)
    {
        List<string> newSubFolderSplit;
        switch (subfolder)
        {
            case null:
                newSubFolderSplit = new List<string> { AnimeInfo.First().PreferredTitle };
                break;
            case string str:
                newSubFolderSplit = new List<string> { str };
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
                throw new LuaScriptException("subfolder returned a value of an unexpected type", string.Empty);
        }

        newSubFolderSplit = newSubFolderSplit
            .Select(f => (removeIllegalChars ? f : f.ReplacePathSegmentChars(replaceIllegalChars)).CleanPathSegment(false)).ToList();
        var newSubfolder = Path.Combine(newSubFolderSplit.ToArray()).NormPath();
        return newSubfolder;
    }

    private IImportFolder GetNewDestination(object? destination)
    {
        IImportFolder? destfolder;
        switch (destination)
        {
            case null:
                destfolder = AvailableFolders
                    // Order by common prefix (stronger version of same drive)
                    .OrderByDescending(f => string.Concat(FileInfo.Path.NormPath()
                        .TakeWhile((ch, i) => i < f.Path.NormPath().Length
                                              && char.ToUpperInvariant(f.Path.NormPath()[i]) == char.ToUpperInvariant(ch))).Length)
                    .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
                if (destfolder is null)
                    throw new LuaRenamerException("could not find an available destination import folder");
                break;
            case string str:
                destfolder = AvailableFolders.FirstOrDefault(f =>
                    string.Equals(f.Name, str, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.Path.NormPath(), str.NormPath(), StringComparison.OrdinalIgnoreCase));
                if (destfolder is null)
                    throw new LuaRenamerException($"could not find destination folder by name or path: {str}");
                break;
            case LuaTable destTable:
                if ((string)destTable[LuaEnv.importfolder._classid] == LuaEnv.importfolder._classidVal)
                    destfolder = AvailableFolders.First(i => i.ID == Convert.ToInt32(destTable[LuaEnv.importfolder.id]));
                else
                    throw new LuaRenamerException($"destination table was not the correct class, assign a table from {LuaEnv.importfolders}");
                break;
            default:
                throw new LuaScriptException(
                    $"destination must be nil or an existing import folder string (name/path), or table (see {LuaEnv.importfolders} variable)",
                    string.Empty);
        }

        if (!destfolder.DropFolderType.HasFlag(DropFolderType.Destination))
            throw new LuaRenamerException($"selected import folder \"{destfolder.Path}\" is not a destination folder, check import folder type");
        return destfolder;
    }

    private (IImportFolder destination, string subfolder)? GetExistingAnimeLocation()
    {
        var availableLocations = AnimeInfo.First().VideoList
            .Where(vl => !string.Equals(vl.Hashes.ED2K, VideoInfo.Hashes.ED2K, StringComparison.OrdinalIgnoreCase))
            .SelectMany(vl => vl.Locations.Select(l => new
            {
                l.ImportFolder,
                SubFolder = SubfolderFromRelativePath(l)
            }))
            .Where(vlp => !string.IsNullOrWhiteSpace(vlp.SubFolder) && vlp.ImportFolder is not null &&
                          (vlp.ImportFolder.DropFolderType.HasFlag(DropFolderType.Destination) ||
                           vlp.ImportFolder.DropFolderType.HasFlag(DropFolderType.Excluded))).ToList();
        var bestLocation = availableLocations.GroupBy(l => l.SubFolder)
            .OrderByDescending(g => g.ToList().Count).Select(g => g.First())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(bestLocation?.SubFolder) || bestLocation.ImportFolder is null) return null;
        return (bestLocation.ImportFolder, bestLocation.SubFolder);
    }

    private static string? SubfolderFromRelativePath(IVideoFile videoFile)
    {
        return Path.GetDirectoryName(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }.Contains(videoFile.RelativePath[0])
            ? videoFile.RelativePath[1..]
            : videoFile.RelativePath);
    }

    private void CheckBadArgs()
    {
        if (string.IsNullOrWhiteSpace(Script.Script))
            throw new LuaRenamerException("Script is empty or null");
        if (Script.Type != RenamerId)
            throw new LuaRenamerException($"Script doesn't match {RenamerId}");
        if (AnimeInfo.Count == 0 || EpisodeInfo.Count == 0)
            throw new LuaRenamerException("No anime and/or episode info");
    }
}
