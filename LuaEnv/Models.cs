// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.LuaEnv;

// The complete env-description graph: every ILuaModel record plus the EnvModel root. Property names and
// declaration order are the schema — they drive both the serialized Lua tables and the generated defs, so
// reordering a property reorders the generated documents. All marshaling happens in ModelTranslator.

public sealed record DateTimeModel : ILuaModel
{
    [LuaField("Year (four digits)")] public required long year { get; init; }
    [LuaField("Month (1-12)")] public required long month { get; init; }
    [LuaField("Day of the month (1-31)")] public required long day { get; init; }
    [LuaField("Day of the year (1-366)")] public required long yday { get; init; }
    [LuaField("Day of the week (1-7, 1 is Sunday)")] public required long wday { get; init; }
    [LuaField("Hour (0-23)")] public required long hour { get; init; }
    [LuaField("Minute (0-59)")] public required long min { get; init; }
    [LuaField("Second (0-59)")] public required long sec { get; init; }
    [LuaField("Is Daylight Saving Time in effect")] public required bool isdst { get; init; }
}

public sealed record TitleModel : ILuaModel
{
    [LuaField("The title text")] public required string name { get; init; }
    [LuaField("Language of the title")] public required TitleLanguage language { get; init; }
    [LuaField("ISO language code")] public required string languagecode { get; init; }
    [LuaField("Type of the title")] public required TitleType type { get; init; }
}

public sealed record SeasonModel : ILuaModel
{
    [LuaField("Season year")] public required long year { get; init; }
    [LuaField("Season aired")] public required YearlySeason season { get; init; }
}

public sealed record RelationModel : ILuaModel
{
    [LuaField("The related anime")] public required AnimeModel anime { get; init; }
    [LuaField("Type of relation between the anime")] public required RelationType type { get; init; }
}

public sealed record AnimeModel : ILuaModel
{
    // Static because the closure is the same for every node; the translator writes the one shared handle
    // onto each table. The ':' method-call syntax is a [LuaField] flag.
    [LuaField("Get the anime title in the specified language", Method = true)]
    public static LuaFunc<AnimeTitleDelegate> getname => LuaFunctions.AnimeGetName;

    [LuaField("First air date of the anime")] public DateTimeModel? airdate { get; init; }
    [LuaField("Last air date of the anime")] public DateTimeModel? enddate { get; init; }
    [LuaField("Average rating of the anime")] public required double rating { get; init; }
    [LuaField("Whether the anime is age-restricted")] public required bool restricted { get; init; }
    [LuaField("Type of the anime (Movie, TVSeries, etc.)")] public required AnimeType type { get; init; }
    [LuaField("The preferred title for the anime")] public required string preferredname { get; init; }
    [LuaField("The default title for the anime")] public required string defaultname { get; init; }
    [LuaField("AniDB anime ID")] public required long id { get; init; }

    [LuaField("All available titles for the anime")]
    public required IReadOnlyList<TitleModel> titles { get; init; }

    [LuaField("Count of episodes by type")]
    public required IReadOnlyDictionary<EpisodeType, long> episodecounts { get; init; }

    [LuaField("Related anime entries, not populated for nested Anime entries")]
    public required IReadOnlyList<RelationModel> relations { get; init; }

    [LuaField("List of studios that produced the anime")]
    public required IReadOnlyList<string> studios { get; init; }

    [LuaField("List of anime series tags")]
    public required IReadOnlyList<string> tags { get; init; }

    [LuaField("List of custom Shoko tags")]
    public required IReadOnlyList<string> customtags { get; init; }

    [LuaField("List of seasons anime aired during")]
    public required IReadOnlyList<SeasonModel> seasons { get; init; }
}

public sealed record HashesModel : ILuaModel
{
    [LuaField("CRC32 hash of the file")] public required string? crc { get; init; }
    [LuaField("MD5 hash of the file")] public required string? md5 { get; init; }
    [LuaField("ED2K hash of the file")] public required string ed2k { get; init; }
    [LuaField("SHA1 hash of the file")] public required string? sha1 { get; init; }
}

public sealed record AudioModel : ILuaModel
{
    [LuaField("Audio compression mode")] public required string compressionmode { get; init; }
    [LuaField("Number of audio channels, may have decimal part '.1'")] public required double channels { get; init; }
    [LuaField("Audio sampling rate in Hz")] public required long samplingrate { get; init; }
    [LuaField("Audio codec name")] public required string codec { get; init; }
    [LuaField("Audio track language")] public required string language { get; init; }
    [LuaField("Audio track title or name")] public required string? title { get; init; }
}

public sealed record VideoModel : ILuaModel
{
    [LuaField("Video height in pixels")] public required long height { get; init; }
    [LuaField("Video width in pixels")] public required long width { get; init; }
    [LuaField("Video codec name")] public required string codec { get; init; }
    [LuaField("Resolution string e.g. '1080p', '720p', etc.")] public required string res { get; init; }
    [LuaField("Video bitrate in bits per second")] public required long bitrate { get; init; }
    [LuaField("Color depth in bits per channel")] public required long bitdepth { get; init; }
    [LuaField("Frame rate in frames per second")] public required double framerate { get; init; }
}

public sealed record MediaModel : ILuaModel
{
    [LuaField("Whether the media file contains chapters")] public required bool chaptered { get; init; }
    [LuaField("Duration of the media in seconds")] public required long duration { get; init; }
    [LuaField("Overall bitrate of the media file")] public required long bitrate { get; init; }

    [LuaField("List of subtitle languages")]
    public required IReadOnlyList<string> sublanguages { get; init; }

    [LuaField("List of audio tracks")]
    public required IReadOnlyList<AudioModel> audio { get; init; }

    [LuaField("Video stream information")] public VideoModel? video { get; init; }
}

public sealed record ReleaseGroupModel : ILuaModel
{
    [LuaField("Full name of the release group")] public required string name { get; init; }
    [LuaField("Abbreviated name or acronym of the release group")] public required string shortname { get; init; }
}

public sealed record AniDbMediaModel : ILuaModel
{
    [LuaField("List of subtitle languages available in the release")]
    public required IReadOnlyList<TitleLanguage> sublanguages { get; init; }

    [LuaField("List of audio languages available in the release")]
    public required IReadOnlyList<TitleLanguage> dublanguages { get; init; }
}

public sealed record AniDbModel : ILuaModel
{
    [LuaField("AniDB file ID")] public required long id { get; init; }
    [LuaField("Whether the release is censored")] public required bool? censored { get; init; }
    [LuaField("Source media of the release e.g. DVD, BD, Web, etc.")] public required string source { get; init; }
    [LuaField("Version number of the release")] public required long version { get; init; }
    [LuaField("Release date of the file")] public DateTimeModel? releasedate { get; init; }
    [LuaField("Description or notes about the release")] public required string? description { get; init; }
    [LuaField("Information about the release group")] public ReleaseGroupModel? releasegroup { get; init; }
    [LuaField("Media information from AniDB")] public required AniDbMediaModel media { get; init; }
}

public sealed record ImportFolderModel : ILuaModel
{
    [LuaField("The Shoko import folder ID")] public required long id { get; init; }
    [LuaField("Name of the import folder")] public required string name { get; init; }
    [LuaField("File system path to the import folder")] public required string location { get; init; }
    [LuaField("Type of the import folder")] public required DropFolderType type { get; init; }
}

public sealed record FileModel : ILuaModel
{
    [LuaField("The name of the file without extension")] public required string name { get; init; }
    [LuaField("The file extension including the dot")] public required string extension { get; init; }
    [LuaField("The full path to the file")] public required string path { get; init; }
    [LuaField("The file size in bytes")] public required long size { get; init; }
    [LuaField("The import folder containing this file")] public required ImportFolderModel importfolder { get; init; }
    [LuaField("The earliest known name of the file")] public required string? earliestname { get; init; }
    [LuaField("Media information (via MediaInfo) for the file")] public MediaModel? media { get; init; }
    [LuaField("AniDB information for the file")] public AniDbModel? anidb { get; init; }
    [LuaField("File hashes")] public required HashesModel hashes { get; init; }
}

public sealed record EpisodeModel : ILuaModel
{
    [LuaField("Get the title in the specified language", Method = true)]
    public static LuaFunc<TitleDelegate> getname => LuaFunctions.GetName;

    [LuaField("Duration of the episode in seconds")] public required long duration { get; init; }
    [LuaField("Episode number")] public required long number { get; init; }
    [LuaField("Type of the episode")] public required EpisodeType type { get; init; }
    [LuaField("Air date of the episode")] public DateTimeModel? airdate { get; init; }
    [LuaField("ID of the anime this episode belongs to")] public required long animeid { get; init; }
    [LuaField("AniDB episode ID")] public required long id { get; init; }

    [LuaField("All available titles for the episode")]
    public required IReadOnlyList<TitleModel> titles { get; init; }

    [LuaField("Episode number type prefix (e.g., '', 'C', 'S', 'T', 'P', 'O')")]
    public required string prefix { get; init; }
}

public sealed record GroupModel : ILuaModel
{
    [LuaField("The name of the group")] public required string? name { get; init; }
    [LuaField("The main anime in the group")] public required AnimeModel mainanime { get; init; }

    [LuaField("All animes in the group")]
    public required IReadOnlyList<AnimeModel> animes { get; init; }
}

public sealed record TmdbShowModel : ILuaModel
{
    [LuaField("Get the title in the specified language", Method = true)]
    public static LuaFunc<TitleDelegate> getname => LuaFunctions.GetName;

    [LuaField("TMDB show ID")] public required long id { get; init; }

    [LuaField("All available titles for the show")]
    public required IReadOnlyList<TitleModel> titles { get; init; }

    [LuaField("Default show title")] public required string? defaultname { get; init; }
    [LuaField("Preferred show title")] public required string? preferredname { get; init; }
    [LuaField("Show rating")] public required double rating { get; init; }
    [LuaField("Whether the show is age-restricted")] public required bool restricted { get; init; }

    [LuaField("List of production studios")]
    public required IReadOnlyList<string> studios { get; init; }

    [LuaField("Total number of episodes")] public required long episodecount { get; init; }
    [LuaField("Air date of the show")] public DateTimeModel? airdate { get; init; }
    [LuaField("End date of the show")] public DateTimeModel? enddate { get; init; }

    [LuaField("List of seasons show aired during")]
    public required IReadOnlyList<SeasonModel> seasons { get; init; }
}

public sealed record TmdbMovieModel : ILuaModel
{
    [LuaField("Get the title in the specified language", Method = true)]
    public static LuaFunc<TitleDelegate> getname => LuaFunctions.GetName;

    [LuaField("TMDB movie ID")] public required long id { get; init; }

    [LuaField("AniDB episode IDs this movie is linked to")]
    public required IReadOnlyList<long> anidbepisodeids { get; init; }

    [LuaField("All available titles for the movie")]
    public required IReadOnlyList<TitleModel> titles { get; init; }

    [LuaField("Default movie title")] public required string? defaultname { get; init; }
    [LuaField("Preferred movie title")] public required string? preferredname { get; init; }
    [LuaField("Movie rating")] public required double rating { get; init; }
    [LuaField("Whether the movie is age-restricted")] public required bool restricted { get; init; }

    [LuaField("List of production studios")]
    public required IReadOnlyList<string> studios { get; init; }

    [LuaField("Air date of the movie")] public DateTimeModel? airdate { get; init; }
}

public sealed record TmdbEpisodeModel : ILuaModel
{
    [LuaField("Get the title in the specified language", Method = true)]
    public static LuaFunc<TitleDelegate> getname => LuaFunctions.GetName;

    [LuaField("TMDB episode ID")] public required long id { get; init; }
    [LuaField("TMDB show ID")] public required long showid { get; init; }

    [LuaField("AniDB episode IDs this TMDB episode is linked to")]
    public required IReadOnlyList<long> anidbepisodeids { get; init; }


    [LuaField("All available titles for the episode")]
    public required IReadOnlyList<TitleModel> titles { get; init; }

    [LuaField("Default episode title")] public required string? defaultname { get; init; }
    [LuaField("Preferred episode title")] public required string? preferredname { get; init; }
    [LuaField("Type of episode")] public required EpisodeType type { get; init; }
    [LuaField("Episode number within the season")] public required long number { get; init; }
    [LuaField("Season number")] public required long? seasonnumber { get; init; }
    [LuaField("Air date of the episode")] public DateTimeModel? airdate { get; init; }
}

public sealed record TmdbModel : ILuaModel
{
    [LuaField("List of TMDB movies related to the file")]
    public required IReadOnlyList<TmdbMovieModel> movies { get; init; }

    [LuaField("List of TMDB shows related to the file")]
    public required IReadOnlyList<TmdbShowModel> shows { get; init; }

    [LuaField("List of TMDB episodes related to the file")]
    public required IReadOnlyList<TmdbEpisodeModel> episodes { get; init; }
}

/// <summary>
/// The Lua environment root. Holds the free functions, the per-file model graph, the user-written output
/// fields, and the enum tables. Excluded from the defs.lua class section; instead it drives env.lua (the
/// non-<see cref="LuaEnumTable{TEnum}"/> fields) and enums.lua (the <see cref="LuaEnumTable{TEnum}"/> ones).
/// </summary>
public sealed record EnvModel : ILuaModel
{
    [LuaField("Returns formatted episode numbers with padding")]
    public required EpisodeNumbersDelegate episode_numbers { get; init; }

    [LuaField("Log with Debug log level")] public required LogDelegate logdebug { get; init; }
    [LuaField("Log with Information log level")] public required LogDelegate log { get; init; }
    [LuaField("Log with Warning log level")] public required LogDelegate logwarn { get; init; }
    [LuaField("Log with Error log level")] public required LogDelegate logerror { get; init; }

    [LuaField("The current file being processed")] public required FileModel file { get; init; }
    [LuaField("The primary anime for the current file")] public required AnimeModel anime { get; init; }

    [LuaField("All anime related to the current file")]
    public required IReadOnlyList<AnimeModel> animes { get; init; }

    [LuaField("The primary episode for the current file")] public required EpisodeModel episode { get; init; }

    [LuaField("All episodes related to the current file")]
    public required IReadOnlyList<EpisodeModel> episodes { get; init; }

    [LuaField("All available import folders")]
    public required IReadOnlyList<ImportFolderModel> importfolders { get; init; }

    [LuaField("The group containing the primary anime")] public GroupModel? group { get; init; }

    [LuaField("All groups containing anime related to the current file")]
    public required IReadOnlyList<GroupModel> groups { get; init; }

    [LuaField("TMDB information for the current file")] public required TmdbModel tmdb { get; init; }

    // Outputs: written by the user script, never serialized into the env (the producers leave them null,
    // so the translator omits the key and env.lua documents them as nil).
    [LuaField("Output: The filename to rename to")]
    public string? filename { get; init; }

    [LuaField("Output: Import folder name / full directory path / ImportFolder that specifies the destination")]
    public LuaUnion<string, ImportFolderModel>? destination { get; init; }

    [LuaField("Output: The subfolder to move the file to, must be an array table if there is more than one directory component")]
    public LuaUnion<string, IReadOnlyList<string>>? subfolder { get; init; }

    [LuaField("Output: Whether to use the existing location of files from the same anime to determine the output destination/subfolder.",
        DefaultValue = "false")]
    public required bool use_existing_anime_location { get; init; }

    [LuaField("Output: Whether to replace illegal characters with their mapped values", DefaultValue = "false")]
    public required bool replace_illegal_chars { get; init; }

    [LuaField("Output: Whether to remove illegal characters entirely", DefaultValue = "false")]
    public required bool remove_illegal_chars { get; init; }

    [LuaField("Output: Whether to skip renaming the file", DefaultValue = "false")]
    public required bool skip_rename { get; init; }

    [LuaField("Output: Whether to skip moving the file", DefaultValue = "false")]
    public required bool skip_move { get; init; }

    [LuaField("Output: Map of illegal characters to their replacements")]
    public required IReadOnlyDictionary<string, string> illegal_chars_map { get; init; }

    // Enum tables. Declared, never assigned — LuaEnumTable<TEnum> carries no data, so the property name is
    // the Lua name and TEnum is the whole payload.
    [LuaField] public LuaEnumTable<DropFolderType> ImportFolderType { get; init; }
    [LuaField] public LuaEnumTable<AnimeType> AnimeType { get; init; }
    [LuaField] public LuaEnumTable<EpisodeType> EpisodeType { get; init; }
    [LuaField] public LuaEnumTable<TitleType> TitleType { get; init; }
    [LuaField] public LuaEnumTable<TitleLanguage> Language { get; init; }
    [LuaField] public LuaEnumTable<RelationType> RelationType { get; init; }
    [LuaField] public LuaEnumTable<YearlySeason> SeasonName { get; init; }
}
