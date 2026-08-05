using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using NLua;
using NLua.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Relocation;

namespace LuaRenamer;

public class Plugin : IPlugin
{
    public Guid ID => Guid.Parse("0c1d69de-a937-59d3-835a-1a0db6aacafc");


    public string Name => nameof(LuaRenamer);

    public string Description => """
        Lua scripting environment for renaming and moving video files. Provides a powerful Lua 5.4 interface for custom file organization.
    """;
}

public class LuaRenamer : IRelocationProvider<LuaRenamerSettings>
{
    private readonly ILogger<LuaRenamer> _logger;

    public LuaRenamer(ILogger<LuaRenamer> logger) => _logger = logger;

    public string Name => nameof(LuaRenamer);

    public string Description => """
        Lua scripting environment for renaming and moving video files. Provides a powerful Lua 5.4 interface for custom file organization.
    """;

    private static string GetNewFilename(object? filename, RelocationContext<LuaRenamerSettings> args, FilePathCleaner filePathCleaner)
    {
        if (filename is not string)
            return args.File.FileName;
        var fileNameWithExt = filename + Path.GetExtension(args.File.FileName);
        return filePathCleaner.CleanPathSegment(fileNameWithExt);
    }

    private static string GetNewSubfolder(object? subfolder, RelocationContext<LuaRenamerSettings> args, FilePathCleaner filePathCleaner)
    {
        List<string> newSubFolderSplit;
        switch (subfolder)
        {
            case null:
                newSubFolderSplit = [args.Series[0].Title];
                break;
            case string str:
                newSubFolderSplit = [str];
                break;
            case LuaTable subfolderTable:
            {
                newSubFolderSplit = [];
                for (var i = 1; subfolderTable[i] is { } val; i++)
                    newSubFolderSplit.Add(val as string ?? throw new LuaRenamerException("subfolder array must only contain strings"));
                break;
            }
            default:
                throw new LuaException("subfolder returned a value of an unexpected type");
        }

        var newSubfolder = Path.Combine(filePathCleaner.CleanPathSegments(newSubFolderSplit.ToArray())).NormPath();
        return newSubfolder;
    }

    private static IManagedFolder GetNewDestination(object? destination, RelocationContext<LuaRenamerSettings> args)
    {
        IManagedFolder? destfolder;
        switch (destination)
        {
            case null:
                destfolder = args.AvailableFolders
                    // Order by common prefix (stronger version of same drive)
                    .OrderByDescending(f => string.Concat(args.File.Path.NormPath()
                        .TakeWhile((ch, i) => i < f.Path.NormPath().Length
                                              && char.ToUpperInvariant(f.Path.NormPath()[i]) == char.ToUpperInvariant(ch))).Length)
                    .FirstOrDefault(f => f.DropFolderType.HasFlag(DropFolderType.Destination));
                if (destfolder is null)
                    throw new LuaRenamerException("could not find an available destination import folder");
                break;
            case string str:
                destfolder = args.AvailableFolders.FirstOrDefault(f =>
                    string.Equals(f.Name, str, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.Path.NormPath(), str.NormPath(), StringComparison.OrdinalIgnoreCase));
                if (destfolder is null)
                    throw new LuaRenamerException($"could not find an available import folder by name or path: \"{str}\"");
                break;
            case LuaTable destTable when destTable[nameof(ImportFolderModel.id)] is not null
                                         && destTable[nameof(ImportFolderModel.name)] is not null:
                destfolder = args.AvailableFolders.FirstOrDefault(i => i.ID == Convert.ToInt32(destTable[nameof(ImportFolderModel.id)])) ??
                             throw new LuaRenamerException($"could not find an available import folder by ID: {destTable[nameof(ImportFolderModel.id)]}");
                break;
            case LuaTable:
                throw new LuaRenamerException("destination table was not an import folder, assign a table from importfolders variable");
            default:
                throw new LuaRenamerException($"destination must be nil, an string (name/path), or a table from importfolders variable");
        }

        if (!destfolder.DropFolderType.HasFlag(DropFolderType.Destination))
            throw new LuaRenamerException($"selected import folder \"{destfolder.Path}\" is not a destination folder, check import folder type");
        return destfolder;
    }

    private static (IManagedFolder destination, string subfolder)? GetExistingAnimeLocation(RelocationContext<LuaRenamerSettings> args)
    {
        var availableLocations = args.Series[0].Videos
            .Where(vl => !string.Equals(vl.ED2K, args.File.Video.ED2K, StringComparison.OrdinalIgnoreCase))
            .SelectMany(vl => vl.Files.Select(l => new
            {
                l.ManagedFolder,
                SubFolder = SubfolderFromRelativePath(l),
            }))
            .Where(vlp => !string.IsNullOrWhiteSpace(vlp.SubFolder) &&
                          (vlp.ManagedFolder.DropFolderType.HasFlag(DropFolderType.Destination) ||
                           vlp.ManagedFolder.DropFolderType.HasFlag(DropFolderType.Excluded))).ToList();
        var bestLocation = availableLocations.GroupBy(l => l.SubFolder)
            .OrderByDescending(g => g.ToList().Count).Select(g => g.First())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(bestLocation?.SubFolder)) return null;
        return (bestLocation.ManagedFolder, bestLocation.SubFolder);
    }

    private static string? SubfolderFromRelativePath(IVideoFile videoFile)
    {
        return Path.GetDirectoryName(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }.Contains(videoFile.RelativePath[0])
            ? videoFile.RelativePath[1..]
            : videoFile.RelativePath);
    }

    public RelocationResult GetPath(RelocationContext<LuaRenamerSettings> args)
    {
        try
        {
            if (args.File.Video is null)
                throw new LuaRenamerException("File did not have video info");
            if (args.Configuration.Script is null)
                throw new LuaRenamerException("Script is null");
            if (args.Series.Count == 0)
                throw new LuaRenamerException("No anime info");
            if (args.Episodes.Count == 0)
                throw new LuaRenamerException("No episode info");

            using var sandbox = new LuaSandbox(LuaScripts.LuaLinq, LuaScripts.Utils);
            new LuaSerializer(sandbox).Serialize(ModelProducers.EnvToModel(args, _logger), sandbox.Env);
            var retVal = sandbox.Run(args.Configuration.Script);
            if (retVal.Length == 2 && retVal[0] is not true && retVal[1] is string errStr)
                throw new LuaRenamerException(errStr);

            var env = sandbox.Env;
            var replaceIllegalChars = env[nameof(EnvModel.replace_illegal_chars)] is true;
            var removeIllegalChars = env[nameof(EnvModel.remove_illegal_chars)] is true;
            var useExistingAnimeLocation = env[nameof(EnvModel.use_existing_anime_location)] is true;
            var skipMove = env[nameof(EnvModel.skip_move)] is true;
            var skipRename = env[nameof(EnvModel.skip_rename)] is true;
            var luaFilename = env[nameof(EnvModel.filename)];
            var luaDestination = env[nameof(EnvModel.destination)];
            var luaSubfolder = env[nameof(EnvModel.subfolder)];
            var illegalCharsOverride = env[nameof(EnvModel.illegal_chars_map)] is LuaTable luaIllegalCharsOverride
                ? sandbox.GetTableDict(luaIllegalCharsOverride)
                    .Where(kvp => kvp is { Key: string, Value: string })
                    .Select(kvp => new KeyValuePair<string, string>((string)kvp.Key, (string)kvp.Value)).ToDictionary()
                : new Dictionary<string, string>();

            var filePathCleaner = new FilePathCleaner(removeIllegalChars, replaceIllegalChars, args.Configuration.PlatformDependentIllegalCharacters,
                illegalCharsOverride);

            var result = new RelocationResult { SkipMove = skipMove, SkipRename = skipRename };

            if (args.MoveEnabled && !skipMove)
                (result.ManagedFolder, result.Path) =
                    (useExistingAnimeLocation ? GetExistingAnimeLocation(args) : null) ??
                    (GetNewDestination(luaDestination, args), GetNewSubfolder(luaSubfolder, args, filePathCleaner));

            if (args.RenameEnabled && !skipRename)
                result.FileName = GetNewFilename(luaFilename, args, filePathCleaner);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{Exception}", e.ToString());
            var st = new StackTrace(e, true);
            var frame = st.GetFrames().FirstOrDefault(f => f.GetFileName() is not null);
            return new()
            {
                Error = new(
                    $"*Error: File: {frame?.GetFileName()} Method: {frame?.GetMethod()?.Name} Line: {frame?.GetFileLineNumber()} | {e.Message}", e),
            };
        }
    }
}
