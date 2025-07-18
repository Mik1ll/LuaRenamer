// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class EnvTable : RootTable
{
    [LuaType("string", Nillable = true)]
    [LuaDescription("Output: The filename to rename to")]
    public static string filename => Get();

    [LuaType("string|ImportFolder", Nillable = true)]
    [LuaDescription("Output: The destination path or ImportFolder to move to")]
    public static string destination => Get();

    [LuaType("string|string[]", Nillable = true)]
    [LuaDescription("Output: The subfolder(s) to create and move to")]
    public static string subfolder => Get();

    [LuaType("boolean", DefaultValue = "false")]
    [LuaDescription("Output: Whether to use the existing anime location")]
    public static string use_existing_anime_location => Get();

    [LuaType("boolean", DefaultValue = "false")]
    [LuaDescription("Output: Whether to replace illegal characters with their mapped values")]
    public static string replace_illegal_chars => Get();

    [LuaType("boolean", DefaultValue = "false")]
    [LuaDescription("Output: Whether to remove illegal characters")]
    public static string remove_illegal_chars => Get();

    [LuaType("boolean", DefaultValue = "false")]
    [LuaDescription("Output: Whether to skip renaming the file")]
    public static string skip_rename => Get();

    [LuaType("boolean", DefaultValue = "false")]
    [LuaDescription("Output: Whether to skip moving the file")]
    public static string skip_move => Get();

    [LuaType("table<string, string>")]
    [LuaDescription("Map of illegal characters to their replacements")]
    public static string illegal_chars_map => Get();

    [LuaType("File")]
    [LuaDescription("The current file being processed")]
    public static FileTable file => new() { Fn = Get() };

    [LuaType("Anime")]
    [LuaDescription("The primary anime for the current file")]
    public static AnimeTable anime => new() { Fn = Get() };

    [LuaType("Anime[]")]
    [LuaDescription("All animes related to the current file")]
    public static ArrayTable<AnimeTable> animes => new() { Fn = Get() };

    [LuaType("Episode")]
    [LuaDescription("The primary episode for the current file")]
    public static EpisodeTable episode => new() { Fn = Get() };

    [LuaType("Episode[]")]
    [LuaDescription("All episodes related to the current file")]
    public static ArrayTable<EpisodeTable> episodes => new() { Fn = Get() };

    [LuaType("ImportFolder[]")]
    [LuaDescription("All available import folders")]
    public static ArrayTable<ImportFolderTable> importfolders => new() { Fn = Get() };

    [LuaType("Group", Nillable = true)]
    [LuaDescription("The group for the current file")]
    public static GroupTable group => new() { Fn = Get() };

    [LuaType("Group[]")]
    [LuaDescription("All available groups")]
    public static ArrayTable<GroupTable> groups => new() { Fn = Get() };

    [LuaType("Tmdb")]
    [LuaDescription("TMDB information for the current file")]
    public static TmdbTable tmdb => new() { Fn = Get() };

    [LuaType("function")]
    [LuaDescription("Returns formatted episode numbers with padding")]
    [LuaParameter("pad", "integer", "The amount of padding to use")]
    [LuaReturnType("string")]
    public static string episode_numbers(string pad) => GetFunc([pad]);

    [LuaType("function")]
    [LuaDescription("Log with Debug log level")]
    [LuaParameter("message", "string", "The message to log")]
    [LuaReturnType("nil")]
    public static string logdebug => Get();

    [LuaType("function")]
    [LuaDescription("Log with Information log level")]
    [LuaParameter("message", "string", "The message to log")]
    [LuaReturnType("nil")]
    public static string log => Get();

    [LuaType("function")]
    [LuaDescription("Log with Warning log level")]
    [LuaParameter("message", "string", "The message to log")]
    [LuaReturnType("nil")]
    public static string logwarn => Get();

    [LuaType("function")]
    [LuaDescription("Log with Error log level")]
    [LuaParameter("message", "string", "The message to log")]
    [LuaReturnType("nil")]
    public static string logerror => Get();
}
