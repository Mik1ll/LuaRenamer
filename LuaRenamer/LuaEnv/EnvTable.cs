// ReSharper disable InconsistentNaming

using System;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class EnvTable : LuaRootTableWriter
{
    internal EnvTable(LuaTable t) : base(t) { }

    [LuaType(LuaTypeNames.function, "Returns formatted episode numbers with padding")]
    [LuaParameter("pad", LuaTypeNames.integer, "The amount of padding to use")]
    [LuaReturnType(LuaTypeNames.@string)]
    public required Func<int, string> episode_numbers { init => Set(value); }

    [LuaType(LuaTypeNames.function, "Log with Debug log level")]
    [LuaParameter("message", LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public required Action<string> logdebug { init => Set(value); }

    [LuaType(LuaTypeNames.function, "Log with Information log level")]
    [LuaParameter("message", LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public required Action<string> log { init => Set(value); }

    [LuaType(LuaTypeNames.function, "Log with Warning log level")]
    [LuaParameter("message", LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public required Action<string> logwarn { init => Set(value); }

    [LuaType(LuaTypeNames.function, "Log with Error log level")]
    [LuaParameter("message", LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public required Action<string> logerror { init => Set(value); }

    [LuaField("The current file being processed")]
    public required LuaRef<FileTable> file { init => Set(value.Table); }

    [LuaField("The primary anime for the current file")]
    public required LuaRef<AnimeTable> anime { init => Set(value.Table); }

    [LuaField("All anime related to the current file")]
    public required LuaArray<LuaRef<AnimeTable>> animes { init => Set(value.Table); }

    [LuaField("The primary episode for the current file")]
    public required LuaRef<EpisodeTable> episode { init => Set(value.Table); }

    [LuaField("All episodes related to the current file")]
    public required LuaArray<LuaRef<EpisodeTable>> episodes { init => Set(value.Table); }

    [LuaField("All available import folders")]
    public required LuaArray<LuaRef<ImportFolderTable>> importfolders { init => Set(value.Table); }

    [LuaField("The group containing the primary anime")]
    public required LuaRef<GroupTable>? group { init => Set(value?.Table); }

    [LuaField("All groups containing anime related to the current file")]
    public required LuaArray<LuaRef<GroupTable>> groups { init => Set(value.Table); }

    [LuaField("TMDB information for the current file")]
    public required LuaRef<TmdbTable> tmdb { init => Set(value.Table); }

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "Output: The filename to rename to", Output = true)]
    public static string filename => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.ImportFolder}|{LuaTypeNames.nil}",
        $"Output: Import folder name / full directory path / {nameof(LuaTypeNames.ImportFolder)} that specifies the destination", Output = true)]
    public static string destination => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.@string}[]|{LuaTypeNames.nil}",
        "Output: The subfolder to move the file to, must be an array table if there is more than one directory component", Output = true)]
    public static string subfolder => Get();

    [LuaField("Output: Whether to use the existing location of files from the same anime to determine the output destination/subfolder.",
        DefaultValue = "false")]
    public required bool use_existing_anime_location { init => Set(value); }

    [LuaField("Output: Whether to replace illegal characters with their mapped values", DefaultValue = "false")]
    public required bool replace_illegal_chars { init => Set(value); }

    [LuaField("Output: Whether to remove illegal characters entirely", DefaultValue = "false")]
    public required bool remove_illegal_chars { init => Set(value); }

    [LuaField("Output: Whether to skip renaming the file", DefaultValue = "false")]
    public required bool skip_rename { init => Set(value); }

    [LuaField("Output: Whether to skip moving the file", DefaultValue = "false")]
    public required bool skip_move { init => Set(value); }

    [LuaField("Output: Map of illegal characters to their replacements")]
    public required LuaMap<string, string> illegal_chars_map { init => Set(value.Table); }
}
