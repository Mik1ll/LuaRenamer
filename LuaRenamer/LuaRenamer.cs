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
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;

namespace LuaRenamer;

[RenamerID(nameof(LuaRenamer))]
public class LuaRenamer : IRenamer<LuaRenamerSettings>
{
    private readonly ILogger<LuaRenamer> _logger;

    public string Name { get; } = nameof(LuaRenamer);
    public string Description { get; } = "Lua scripting environment for renaming/moving. Written by Mikill(Discord)/Mik1ll(Github).";
    public bool SupportsMoving { get; } = true;
    public bool SupportsRenaming { get; } = true;

    public LuaRenamerSettings? DefaultSettings
    {
        get
        {
            var defaultFile = new FileInfo(Path.Combine(LuaContext.LuaPath, "default.lua"));
            if (defaultFile.Exists)
            {
                using var text = defaultFile.OpenText();
                return new LuaRenamerSettings { Script = text.ReadToEnd() };
            }

            return null;
        }
    }

    public IVideoFile FileInfo { get; private set; } = null!;
    public IVideo VideoInfo { get; private set; } = null!;
    public string Script { get; private set; } = null!;
    public IList<IShokoGroup> GroupInfo { get; private set; } = null!;
    public IList<IEpisode> EpisodeInfo { get; private set; } = null!;
    public IList<ISeries> AnimeInfo { get; private set; } = null!;
    public List<IImportFolder> AvailableFolders { get; private set; } = null!;
    public bool Rename { get; set; }
    public bool Move { get; set; }


    public LuaRenamer(ILogger<LuaRenamer> logger)
    {
        _logger = logger;
    }

    public void SetupArgs(RelocationEventArgs<LuaRenamerSettings> args)
    {
        FileInfo = args.File;
        VideoInfo = args.File.Video ?? throw new LuaRenamerException("File did not have video info");
        AnimeInfo = args.Series.Select(s => s.AnidbAnime).ToList();
        EpisodeInfo = args.Episodes.Select(se => se.AnidbEpisode).ToList();
        GroupInfo = args.Groups.ToList();
        Script = args.Settings.Script;
        AvailableFolders = args.AvailableFolders.ToList();
        Move = args.MoveEnabled;
        Rename = args.RenameEnabled;


        if (string.IsNullOrWhiteSpace(Script))
            throw new LuaRenamerException("Script is empty or null");
        if (AnimeInfo.Count == 0)
            throw new LuaRenamerException("No anime info");
        if (EpisodeInfo.Count == 0)
            throw new LuaRenamerException("No episode info");
    }


    public RelocationResult GetInfo()
    {
        using var lua = new LuaContext(_logger, this);
        var env = lua.RunSandboxed();
        var replaceIllegalChars = (bool)env[LuaEnv.replace_illegal_chars];
        var removeIllegalChars = (bool)env[LuaEnv.remove_illegal_chars];
        var useExistingAnimeLocation = (bool)env[LuaEnv.use_existing_anime_location];
        var skipMove = (bool)env[LuaEnv.skip_move];
        var skipRename = (bool)env[LuaEnv.skip_rename];
        env.TryGetValue(LuaEnv.filename, out var luaFilename);
        env.TryGetValue(LuaEnv.destination, out var luaDestination);
        env.TryGetValue(LuaEnv.subfolder, out var luaSubfolder);

        var (destination, subfolder) = Move && !skipMove
            ? (useExistingAnimeLocation ? GetExistingAnimeLocation() : null) ??
              (GetNewDestination(luaDestination), GetNewSubfolder(luaSubfolder, replaceIllegalChars, removeIllegalChars))
            : (null, null);

        var filename = Rename && !skipRename
            ? luaFilename is string f
                ? (removeIllegalChars ? f : f.ReplacePathSegmentChars(replaceIllegalChars)).CleanPathSegment(true) + Path.GetExtension(FileInfo.FileName)
                : FileInfo.FileName
            : null;

        return new RelocationResult { DestinationImportFolder = destination, Path = subfolder, FileName = filename };
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
                    throw new LuaRenamerException($"could not find an available import folder by name or path: \"{str}\"");
                break;
            case LuaTable destTable:
                if ((string)destTable[LuaEnv.importfolder._classid] == LuaEnv.importfolder._classidVal)
                {
                    destfolder = AvailableFolders.FirstOrDefault(i => i.ID == Convert.ToInt32(destTable[LuaEnv.importfolder.id]));
                    if (destfolder is null)
                        throw new LuaRenamerException($"could not find an available import folder by ID: {destTable[LuaEnv.importfolder.id]}");
                }
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
        var availableLocations = AnimeInfo.First().Videos
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


    public RelocationResult GetNewPath(RelocationEventArgs<LuaRenamerSettings> args)
    {
        try
        {
            SetupArgs(args);
            var result = GetInfo();
            return result;
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var frame = st.GetFrames().FirstOrDefault(f => f.GetFileName() is not null);
            return new RelocationResult
            {
                Error = new RelocationError(
                    $"*Error: File: {frame?.GetFileName()} Method: {frame?.GetMethod()?.Name} Line: {frame?.GetFileLineNumber()} | {e.Message}", e)
            };
        }
    }
}
