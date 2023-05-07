using System;
using System.Collections;
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
    private static readonly Type? Repofact = Utils.GetTypeFromAssemblies("Shoko.Server.Repositories.RepoFactory");
    private static readonly dynamic? VideoLocalRepo = Repofact?.GetProperty("VideoLocal")?.GetValue(null);
    private static readonly dynamic? ImportFolderRepo = Repofact?.GetProperty("ImportFolder")?.GetValue(null);

    internal static string ScriptCache = string.Empty;
    internal static readonly Dictionary<int, (DateTime setTIme, string filename, IImportFolder destination, string subfolder)> ResultCache = new();

    public IVideoFile FileInfo { get; private set; } = null!;
    public IRenameScript Script { get; private set; } = null!;
    public IList<IGroup> GroupInfo { get; private set; } = null!;
    public IList<IEpisode> EpisodeInfo { get; private set; } = null!;
    public IList<IAnime> AnimeInfo { get; private set; } = null!;
    public List<IImportFolder> AvailableFolders { get; private set; } = null!;

    public bool SkipRename { get; private set; }
    public bool SkipMove { get; private set; }


    public LuaRenamer(ILogger<LuaRenamer> logger)
    {
        _logger = logger;
    }

    private (string filename, IImportFolder destination, string subfolder)? CheckCache()
    {
        var videoFileId = FileInfo.VideoFileID;
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
            return (null, $"*Error: {e.Message}");
        }
    }

    private void SetupArgs(RenameEventArgs args)
    {
        FileInfo = args.FileInfo;
        AnimeInfo = args.AnimeInfo;
        EpisodeInfo = args.EpisodeInfo;
        GroupInfo = args.GroupInfo;
        Script = args.Script;
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        AvailableFolders ??= new List<IImportFolder>();
    }

    public void SetupArgs(MoveEventArgs args)
    {
        FileInfo = args.FileInfo;
        AnimeInfo = args.AnimeInfo;
        EpisodeInfo = args.EpisodeInfo;
        GroupInfo = args.GroupInfo;
        Script = args.Script;
        AvailableFolders = args.AvailableFolders;
    }

    public (string filename, IImportFolder destination, string subfolder)? GetInfo()
    {
        if (CheckCache() is { } cacheHit)
        {
            _logger.LogInformation("Returning rename/move result from cache");
            return cacheHit;
        }
        AvailableFolders = ((IEnumerable?)ImportFolderRepo?.GetAll())?.Cast<IImportFolder>().ToList() ?? AvailableFolders;
        using var lua = new LuaContext(_logger, this);
        var env = lua.RunSandboxed();
        var replaceIllegalChars = (bool)env[LuaEnv.replace_illegal_chars];
        var removeIllegalChars = (bool)env[LuaEnv.remove_illegal_chars];
        var useExistingAnimeLocation = (bool)env[LuaEnv.use_existing_anime_location];
        env.TryGetValue(LuaEnv.filename, out var luaFilename);
        env.TryGetValue(LuaEnv.destination, out var luaDestination);
        env.TryGetValue(LuaEnv.subfolder, out var luaSubfolder);
        env.TryGetValue(LuaEnv.skip_rename, out var luaSkipRename);
        SkipRename = (bool?)luaSkipRename ?? false;
        env.TryGetValue(LuaEnv.skip_move, out var luaSkipMove);
        SkipMove = (bool?)luaSkipMove ?? false;

        IImportFolder? destination;
        string? subfolder;
        string filename;
        if (SkipMove)
        {
            destination = AvailableFolders.First(f => FileInfo.FilePath.NormPath().StartsWith(f.Location.NormPath()));
            subfolder = Path.GetDirectoryName(FileInfo.FilePath)!.Substring(destination.Location.NormPath().Length + 1);
        }
        else
            (destination, subfolder) = (useExistingAnimeLocation ? GetExistingAnimeLocation() : null) ??
                                       (GetNewDestination(luaDestination), GetNewSubfolder(luaSubfolder, replaceIllegalChars, removeIllegalChars));

        if (SkipRename)
            filename = FileInfo.Filename;
        else
            filename = luaFilename is string f
                ? (removeIllegalChars ? f : f.ReplacePathSegmentChars(replaceIllegalChars)).CleanPathSegment(true) + Path.GetExtension(FileInfo.Filename)
                : FileInfo.Filename;

        if (filename is null || string.IsNullOrWhiteSpace(subfolder)) return null;
        ResultCache.Add(FileInfo.VideoFileID, (DateTime.UtcNow, filename, destination, subfolder));
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
                throw new LuaScriptException("subfolder must be an array of path segments or nil", string.Empty);
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
                    .OrderByDescending(f => string.Concat(FileInfo.FilePath.NormPath()
                        .TakeWhile((ch, i) => i < f.Location.NormPath().Length
                                              && char.ToUpperInvariant(f.Location.NormPath()[i]) == char.ToUpperInvariant(ch))).Length)
                    .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
                if (destfolder is null)
                    throw new ArgumentException("could not find an available destination import folder");
                break;
            case string str:
                destfolder = AvailableFolders.FirstOrDefault(f =>
                    string.Equals(f.Name, str, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.Location.NormPath(), str.NormPath(), StringComparison.OrdinalIgnoreCase));
                if (destfolder is null)
                    throw new ArgumentException($"could not find destination folder by name or path: {str}");
                break;
            case LuaTable destTable:
                if ((string)destTable[LuaEnv.importfolder._classid] == LuaEnv.importfolder._classidVal)
                    destfolder = AvailableFolders[Convert.ToInt32(destTable[LuaEnv.importfolder._index])];
                else
                    throw new ArgumentException($"destination table was not the correct class, assign a table from {LuaEnv.importfolders}");
                break;
            default:
                throw new LuaScriptException(
                    $"destination must be nil or an existing import folder string (name/path), or table (see {LuaEnv.importfolders} variable)",
                    string.Empty);
        }
        if (!destfolder.DropFolderType.HasFlag(DropFolderType.Destination))
            throw new ArgumentException($"selected import folder \"{destfolder.Location}\" is not a destination folder, check import folder type");
        return destfolder;
    }

    private (IImportFolder destination, string subfolder)? GetExistingAnimeLocation()
    {
        if (VideoLocalRepo is null || ImportFolderRepo is null) return null;
        IImportFolder? oldFld = null;
        var lastFileLocation = ((IEnumerable<dynamic>)VideoLocalRepo.GetByAniDBAnimeID(AnimeInfo.First().AnimeID))
            .Where(vl => !string.Equals(vl.CRC32, FileInfo.Hashes.CRC, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(vl => vl.DateTimeUpdated)
            .Select(vl => vl.GetBestVideoLocalPlace())
            .FirstOrDefault(vlp => (oldFld = (IImportFolder)ImportFolderRepo.GetByID(vlp.ImportFolderID)) is not null &&
                                   (oldFld.DropFolderType.HasFlag(DropFolderType.Destination) ||
                                    oldFld.DropFolderType.HasFlag(DropFolderType.Excluded)));
        if (oldFld is null || lastFileLocation is null) return null;
        var subFld = Path.GetDirectoryName((string)lastFileLocation.FilePath);
        if (subFld is null) return null;
        return (oldFld, subFld);
    }

    private void CheckBadArgs()
    {
        if (string.IsNullOrWhiteSpace(Script.Script))
            throw new ArgumentException("Script is empty or null");
        if (Script.Type != RenamerId)
            throw new ArgumentException($"Script doesn't match {RenamerId}");
        if (AnimeInfo.Count == 0 || EpisodeInfo.Count == 0)
            throw new ArgumentException("No anime and/or episode info");
    }
}
