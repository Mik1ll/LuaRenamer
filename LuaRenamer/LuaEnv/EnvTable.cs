// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;

namespace LuaRenamer.LuaEnv;

public partial class EnvTable : LuaRootTableWriter
{
    internal EnvTable(LuaTable t) : base(t) { }

    [LuaField("Returns formatted episode numbers with padding")]
    public required LuaFunctionRef<EpisodeNumbersDelegate> episode_numbers { init => Set(value.Value); }

    [LuaField("Log with Debug log level")]
    public required LuaFunctionRef<LogDelegate> logdebug { init => Set(value.Value); }

    [LuaField("Log with Information log level")]
    public required LuaFunctionRef<LogDelegate> log { init => Set(value.Value); }

    [LuaField("Log with Warning log level")]
    public required LuaFunctionRef<LogDelegate> logwarn { init => Set(value.Value); }

    [LuaField("Log with Error log level")]
    public required LuaFunctionRef<LogDelegate> logerror { init => Set(value.Value); }

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

    [LuaField("Output: The filename to rename to", Output = true)]
    public static string? filename { get; }

    [LuaField($"Output: Import folder name / full directory path / {nameof(LuaTypeNames.ImportFolder)} that specifies the destination", Output = true)]
    public static LuaUnion<string, LuaRef<ImportFolderTable>>? destination { get; }

    [LuaField("Output: The subfolder to move the file to, must be an array table if there is more than one directory component", Output = true)]
    public static LuaUnion<string, LuaArray<string>>? subfolder { get; }

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
