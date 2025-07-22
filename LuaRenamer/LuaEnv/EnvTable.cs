// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class EnvTable : RootTable
{
    [LuaType(LuaTypeNames.function, "Returns formatted episode numbers with padding")]
    [LuaParameter(nameof(pad), LuaTypeNames.integer, "The amount of padding to use")]
    [LuaReturnType(LuaTypeNames.@string)]
    public static string episode_numbers(string pad) => GetFunc([pad]);

    [LuaType(LuaTypeNames.function, "Log with Debug log level")]
    [LuaParameter(nameof(message), LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public static string logdebug(string message) => GetFunc([message]);

    [LuaType(LuaTypeNames.function, "Log with Information log level")]
    [LuaParameter(nameof(message), LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public static string log(string message) => GetFunc([message]);

    [LuaType(LuaTypeNames.function, "Log with Warning log level")]
    [LuaParameter(nameof(message), LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public static string logwarn(string message) => GetFunc([message]);

    [LuaType(LuaTypeNames.function, "Log with Error log level")]
    [LuaParameter(nameof(message), LuaTypeNames.@string, "The message to log")]
    [LuaReturnType(LuaTypeNames.nil)]
    public static string logerror(string message) => GetFunc([message]);

    [LuaType(LuaTypeNames.File, "The current file being processed")]
    public static FileTable file => new() { Fn = Get() };

    [LuaType(LuaTypeNames.Anime, "The primary anime for the current file")]
    public static AnimeTable anime => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Anime}[]", "All anime related to the current file")]
    public static ArrayTable<AnimeTable> animes => new() { Fn = Get() };

    [LuaType(LuaTypeNames.Episode, "The primary episode for the current file")]
    public static EpisodeTable episode => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Episode}[]", "All episodes related to the current file")]
    public static ArrayTable<EpisodeTable> episodes => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.ImportFolder}[]", "All available import folders")]
    public static ArrayTable<ImportFolderTable> importfolders => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Group}|{LuaTypeNames.nil}", "The group containing the primary anime")]
    public static GroupTable group => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.Group}[]", "All groups containing anime related to the current file")]
    public static ArrayTable<GroupTable> groups => new() { Fn = Get() };

    [LuaType(LuaTypeNames.Tmdb, "TMDB information for the current file")]
    public static TmdbTable tmdb => new() { Fn = Get() };

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.nil}", "Output: The filename to rename to")]
    public static string filename => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.ImportFolder}|{LuaTypeNames.nil}",
        $"Output: Import folder name / full directory path / {nameof(LuaTypeNames.ImportFolder)} that specifies the destination")]
    public static string destination => Get();

    [LuaType($"{LuaTypeNames.@string}|{LuaTypeNames.@string}[]|{LuaTypeNames.nil}",
        "Output: The subfolder to move the file to, must be an array table if there is more than one directory component")]
    public static string subfolder => Get();

    [LuaType(LuaTypeNames.boolean,
        "Output: Whether to use the existing location of files from the same anime to determine the output destination/subfolder.",
        DefaultValue = "false")]
    public static string use_existing_anime_location => Get();

    [LuaType(LuaTypeNames.boolean, "Output: Whether to replace illegal characters with their mapped values", DefaultValue = "false")]
    public static string replace_illegal_chars => Get();

    [LuaType(LuaTypeNames.boolean, "Output: Whether to remove illegal characters entirely", DefaultValue = "false")]
    public static string remove_illegal_chars => Get();

    [LuaType(LuaTypeNames.boolean, "Output: Whether to skip renaming the file", DefaultValue = "false")]
    public static string skip_rename => Get();

    [LuaType(LuaTypeNames.boolean, "Output: Whether to skip moving the file", DefaultValue = "false")]
    public static string skip_move => Get();

    [LuaType($"{LuaTypeNames.table}<{LuaTypeNames.@string}, {LuaTypeNames.@string}>", "Output: Map of illegal characters to their replacements")]
    public static string illegal_chars_map => Get();
}
