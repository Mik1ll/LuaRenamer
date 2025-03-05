using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using NLua;
using NLua.Exceptions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace LuaRenamer;

[RenamerID(nameof(LuaRenamer))]
public class LuaRenamer : IRenamer<LuaRenamerSettings>
{
    private readonly ILogger<LuaRenamer> _logger;

    public LuaRenamer(ILogger<LuaRenamer> logger) => _logger = logger;

    public string Name => nameof(LuaRenamer);
    public string Description => "Lua scripting environment for renaming/moving. Written by Mikill(Discord)/Mik1ll(Github).";
    public bool SupportsMoving => true;
    public bool SupportsRenaming => true;

    public LuaRenamerSettings? DefaultSettings
    {
        get
        {
            var defaultFile = new FileInfo(Path.Combine(LuaContext.LuaPath, "default.lua"));
            if (!defaultFile.Exists) return null;
            using var text = defaultFile.OpenText();
            return new() { Script = text.ReadToEnd() };
        }
    }

    private static string GetNewFilename(object? filename, RelocationEventArgs<LuaRenamerSettings> args, bool removeIllegalChars, bool replaceIllegalChars)
    {
        if (filename is not string)
            return args.File.FileName;
        var fileNameWithExt = filename + Path.GetExtension(args.File.FileName);
        return fileNameWithExt.CleanPathSegment(removeIllegalChars, replaceIllegalChars);
    }

    private static string GetNewSubfolder(object? subfolder, RelocationEventArgs<LuaRenamerSettings> args, bool replaceIllegalChars, bool removeIllegalChars)
    {
        List<string> newSubFolderSplit;
        switch (subfolder)
        {
            case null:
                newSubFolderSplit = [args.Series[0].PreferredTitle];
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

        var newSubfolder = Path.Combine(newSubFolderSplit.Select(f => f.CleanPathSegment(removeIllegalChars, replaceIllegalChars)).ToArray()).NormPath();
        return newSubfolder;
    }

    private static IImportFolder GetNewDestination(object? destination, RelocationEventArgs<LuaRenamerSettings> args)
    {
        IImportFolder? destfolder;
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
            case LuaTable destTable:
                if ((string)destTable[nameof(ImportFolderTable._classid)] == ImportFolderTable._classidVal)
                    destfolder = args.AvailableFolders.FirstOrDefault(i => i.ID == Convert.ToInt32(destTable[nameof(ImportFolderTable.id)])) ??
                                 throw new LuaRenamerException($"could not find an available import folder by ID: {destTable[nameof(ImportFolderTable.id)]}");
                else
                    throw new LuaRenamerException($"destination table was not the correct class, assign a table from {EnvTable.Inst.importfolders} variable");
                break;
            default:
                throw new LuaRenamerException($"destination must be nil, an string (name/path), or a table from {EnvTable.Inst.importfolders} variable");
        }

        if (!destfolder.DropFolderType.HasFlag(DropFolderType.Destination))
            throw new LuaRenamerException($"selected import folder \"{destfolder.Path}\" is not a destination folder, check import folder type");
        return destfolder;
    }

    private static (IImportFolder destination, string subfolder)? GetExistingAnimeLocation(RelocationEventArgs<LuaRenamerSettings> args)
    {
        var availableLocations = args.Series[0].Videos
            .Where(vl => !string.Equals(vl.Hashes.ED2K, args.File.Video.Hashes.ED2K, StringComparison.OrdinalIgnoreCase))
            .SelectMany(vl => vl.Locations.Select(l => new
            {
                l.ImportFolder,
                SubFolder = SubfolderFromRelativePath(l),
            }))
            .Where(vlp => !string.IsNullOrWhiteSpace(vlp.SubFolder) &&
                          (vlp.ImportFolder.DropFolderType.HasFlag(DropFolderType.Destination) ||
                           vlp.ImportFolder.DropFolderType.HasFlag(DropFolderType.Excluded))).ToList();
        var bestLocation = availableLocations.GroupBy(l => l.SubFolder)
            .OrderByDescending(g => g.ToList().Count).Select(g => g.First())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(bestLocation?.SubFolder)) return null;
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
            if (args.File.Video is null)
                throw new LuaRenamerException("File did not have video info");
            if (args.Settings.Script is null)
                throw new LuaRenamerException("Script is null");
            if (args.Series.Count == 0)
                throw new LuaRenamerException("No anime info");
            if (args.Episodes.Count == 0)
                throw new LuaRenamerException("No episode info");

            using var lua = new LuaContext(_logger, args);
            var env = lua.RunSandboxed();
            var replaceIllegalChars = env[nameof(EnvTable.replace_illegal_chars)] is true;
            var removeIllegalChars = env[nameof(EnvTable.remove_illegal_chars)] is true;
            var useExistingAnimeLocation = env[nameof(EnvTable.use_existing_anime_location)] is true;
            var skipMove = env[nameof(EnvTable.skip_move)] is true;
            var skipRename = env[nameof(EnvTable.skip_rename)] is true;
            var luaFilename = env[nameof(EnvTable.filename)];
            var luaDestination = env[nameof(EnvTable.destination)];
            var luaSubfolder = env[nameof(EnvTable.subfolder)];

            var result = new RelocationResult { SkipMove = skipMove, SkipRename = skipRename };

            if (args.MoveEnabled && !skipMove)
                (result.DestinationImportFolder, result.Path) =
                    (useExistingAnimeLocation ? GetExistingAnimeLocation(args) : null) ??
                    (GetNewDestination(luaDestination, args), GetNewSubfolder(luaSubfolder, args, replaceIllegalChars, removeIllegalChars));

            if (args.RenameEnabled && !skipRename)
                result.FileName = GetNewFilename(luaFilename, args, removeIllegalChars, replaceIllegalChars);

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
