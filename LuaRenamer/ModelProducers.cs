using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LuaRenamer.LuaEnv;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Relocation;
using Shoko.Abstractions.Video.Release;

namespace LuaRenamer;

/// <summary>
/// Maps Shoko's host abstractions to the plain <see cref="ILuaModel"/> graph that <see cref="LuaSerializer"/>
/// materializes. Produces decoupled models rather than mutating a live LuaTable, and carries no
/// reference-dedup cache (intentionally — the graph terminates because nested relation anime are
/// built with <c>includeRelations: false</c>).
/// </summary>
/// <remarks>
/// Lives in the plugin project, not LuaEnv: this is the Shoko-facing mapping layer, so keeping it here leaves
/// LuaEnv depending on Shoko's enum types alone rather than its whole metadata interface graph.
/// <c>getname</c> is a pure <see cref="AnimeGetName"/>/<see cref="TitleGetName"/> descriptor of the shared
/// title-resolver closure; <see cref="LuaSerializer"/> mints the live Lua handle from it, so the producers
/// carry no live Lua wiring.
/// </remarks>
public static class ModelProducers
{
    // ---- env root ------------------------------------------------------------------------------

    /// <summary>
    /// Builds the whole <see cref="EnvModel"/> graph from the relocation <paramref name="args"/>. Everything
    /// host-derived is worked out here: the primary series/episode, settings off
    /// <see cref="RelocationContext{T}.Configuration"/>, illegal-char defaults and episode prefixes off
    /// <see cref="FilePathCleaner"/>/<see cref="Utils"/>, and the free functions the env exposes to user
    /// scripts (<c>episode_numbers</c> and the <c>log*</c> family, the latter bound to <paramref name="logger"/>).
    /// </summary>
    public static EnvModel EnvToModel(RelocationContext<LuaRenamerSettings> args, ILogger logger)
    {
        var primarySeries = PrimarySeries(args);
        var primaryEpisode = args.Episodes.Where(e => e.AnidbEpisode.SeriesID == primarySeries.AnidbAnimeID)
            .OrderBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.Type)
            .ThenBy(e => e.EpisodeNumber)
            .First();

        var animes = args.Series
            .OrderBy(s => s.AnidbAnimeID != primarySeries.AnidbAnimeID)
            .ThenBy(s => s.AnidbAnimeID)
            .Select(series => AnimeToModel(series.AnidbAnime)).ToList();
        var episodes = args.Episodes
            .OrderBy(e => e.AnidbEpisodeID != primaryEpisode.AnidbEpisodeID)
            .ThenBy(e => e.AnidbEpisode.SeriesID)
            .ThenBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.AnidbEpisode.Type)
            .ThenBy(e => e.AnidbEpisode.EpisodeNumber)
            .Select(e => EpisodeToModel(e.AnidbEpisode, Utils.EpPrefix[e.AnidbEpisode.Type])).ToList();
        // Groups the primary series actually belongs to come first (what EnvModel.group documents),
        // then the group whose *main* series is the primary one. Both comparisons are in Shoko id
        // space — MainSeriesID is a Shoko series id, not an AniDB anime id.
        var groups = args.Groups
            .OrderBy(g => !g.AllSeries.Any(s => s.ID == primarySeries.ID))
            .ThenBy(g => g.MainSeriesID != primarySeries.ID)
            .Select(GroupToModel).ToList();

        return new EnvModel
        {
            episode_numbers = pad => EpisodeNumbers(args, primarySeries, pad),
            // ReSharper disable TemplateIsNotCompileTimeConstantProblem
            logdebug = message => logger.LogDebug(message),
            log = message => logger.LogInformation(message),
            logwarn = message => logger.LogWarning(message),
            logerror = message => logger.LogError(message),
            // ReSharper restore TemplateIsNotCompileTimeConstantProblem
            replace_illegal_chars = args.Configuration.ReplaceIllegalCharacters,
            remove_illegal_chars = args.Configuration.RemoveIllegalCharacters,
            use_existing_anime_location = args.Configuration.UseExistingAnimeLocation,
            skip_rename = false,
            skip_move = false,
            illegal_chars_map = FilePathCleaner.ReplaceMapDefaults,
            animes = animes,
            anime = animes[0],
            file = FileToModel(args.File),
            episodes = episodes,
            episode = episodes[0],
            importfolders = args.AvailableFolders.Select(ImportFolderToModel).ToList(),
            groups = groups,
            group = groups.Count > 0 ? groups[0] : null,
            tmdb = TmdbToModel(
                primarySeries.TmdbMovies,
                primarySeries.TmdbShows,
                args.Episodes.Where(e => e.SeriesID == primarySeries.ID).SelectMany(e => e.TmdbEpisodes)),
            ImportFolderType = EnumTable<DropFolderType>(),
            AnimeType = EnumTable<AnimeType>(),
            EpisodeType = EnumTable<EpisodeType>(),
            TitleType = EnumTable<TitleType>(),
            Language = EnumTable<TitleLanguage>(),
            RelationType = EnumTable<RelationType>(),
            SeasonName = EnumTable<YearlySeason>(),
        };
    }

    /// <summary>
    /// The primary series for a relocation: the lowest AniDB anime id. The single definition of "primary" —
    /// <see cref="EnvToModel"/> and <see cref="LuaRenamer"/>'s move/rename fallbacks all resolve it through
    /// here rather than trusting the order <see cref="RelocationContext{T}.Series"/> arrives in.
    /// </summary>
    /// <remarks>Throws when the context has no series; callers rely on the emptiness guard in
    /// <see cref="LuaRenamer.GetPath"/>.</remarks>
    public static IShokoSeries PrimarySeries(RelocationContext<LuaRenamerSettings> args) =>
        args.Series.OrderBy(s => s.AnidbAnimeID).First();

    /// <summary>
    /// The Shoko series title, falling back to the AniDB one when blank. Backs both
    /// <c>anime.preferredname</c> and the default subfolder, so the two never disagree.
    /// </summary>
    public static string PreferredName(IAnidbAnime anime, IShokoSeries? series) =>
        string.IsNullOrWhiteSpace(series?.Title) ? anime.Title : series.Title;

    /// <summary>
    /// Backs the env's <c>episode_numbers</c> free function: the primary series' episode numbers, zero-padded
    /// to <paramref name="pad"/> digits, prefixed by type and collapsed into ranges.
    /// </summary>
    private static string EpisodeNumbers(RelocationContext<LuaRenamerSettings> args, IShokoSeries primarySeries, long pad) =>
        string.Join(' ', args.Episodes.Select(se => se.AnidbEpisode)
            .Where(e => e.SeriesID == primarySeries.AnidbAnimeID)
            .OrderBy(e => e.Type).ThenBy(e => e.EpisodeNumber)
            .Select((e, i) => (e.Type, RangeId: e.EpisodeNumber - i, Num: e.EpisodeNumber)) // RangeId effectively groups sequences of numbers
            .GroupBy(x => (x.Type, x.RangeId))
            .Select(g => g.First().Num is var fn && g.Last().Num is var ln && Utils.EpPrefix[g.Key.Type] is var pre && "D" + pad is var fmt && fn == ln
                ? $"{pre}{fn.ToString(fmt)}"
                : $"{pre}{fn.ToString(fmt)}-{ln.ToString(fmt)}"));

    // ---- shared leaf mappers -------------------------------------------------------------------

    private static TitleModel TitleToModel(ITitle title) => new()
    {
        name = title.Value,
        language = title.Language,
        languagecode = title.LanguageCode,
        type = title.Type,
    };

    private static SeasonModel SeasonToModel((int Year, YearlySeason Season) season) => new()
    {
        year = season.Year,
        season = season.Season,
    };

    private static DateTimeModel? DateTimeToModel(DateTime? dateTime)
    {
        if (dateTime is not { } dt)
            return null;
        return new DateTimeModel
        {
            year = dt.Year,
            month = dt.Month,
            day = dt.Day,
            yday = dt.DayOfYear,
            wday = (long)dt.DayOfWeek + 1,
            hour = dt.Hour,
            min = dt.Minute,
            sec = dt.Second,
            isdst = dt.IsDaylightSavingTime(),
        };
    }

    // ---- enum tables ---------------------------------------------------------------------------

    /// <summary>
    /// Builds the identity name→name map for an enum.
    /// The serializer marshals every key/value to its enum name, giving the Lua <c>{ Name = "Name", ... }</c>
    /// table. <see cref="Enumerable.Distinct{TSource}(IEnumerable{TSource})"/> collapses aliased values to the
    /// one canonical name <see cref="Enum.GetName(Type, object)"/> returns.
    /// </summary>
    public static IReadOnlyDictionary<T, T> EnumTable<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Distinct().ToDictionary(v => v, v => v);

    // ---- anime ---------------------------------------------------------------------------------

    public static AnimeModel AnimeToModel(IAnidbAnime anime, bool includeRelations = true)
    {
        ArgumentNullException.ThrowIfNull(anime);
        var series = anime.ShokoSeries.FirstOrDefault();
        return new AnimeModel
        {
            getname = new AnimeGetName(),
            airdate = DateTimeToModel(anime.AirDate?.ToDateTime()),
            enddate = DateTimeToModel(anime.EndDate?.ToDateTime()),
            rating = anime.Rating,
            restricted = anime.Restricted,
            type = anime.Type,
            preferredname = PreferredName(anime, series),
            defaultname = string.IsNullOrWhiteSpace(series?.DefaultTitle.Value) ? anime.DefaultTitle.Value : series.DefaultTitle.Value,
            id = anime.ID,
            titles = anime.Titles.OrderBy(t => t.Value).Select(TitleToModel).ToList(),
            studios = anime.Studios.Select(st => st.Name).ToList(),
            episodecounts = Enum.GetValues<EpisodeType>().Distinct().ToDictionary(ep => ep, ep => (long)anime.EpisodeCounts[ep]),
            relations = includeRelations
                ? anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID).Select(RelationToModel).ToList()
                : [],
            tags = anime.Tags.Select(t => t.Name).ToList(),
            customtags = (series?.Tags.Select(t => t.Name) ?? []).ToList(),
            seasons = anime.YearlySeasons.Select(SeasonToModel).ToList(),
        };
    }

    private static RelationModel RelationToModel(IRelatedMetadata<ISeries, ISeries> relation) => new()
    {
        // nested anime gets includeRelations: false so the graph terminates without a dedup cache.
        anime = AnimeToModel((relation.Related as IAnidbAnime)!, includeRelations: false),
        type = relation.RelationType,
    };

    // ---- file / media --------------------------------------------------------------------------

    public static FileModel FileToModel(IVideoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new FileModel
        {
            name = Path.GetFileNameWithoutExtension(file.FileName),
            extension = Path.GetExtension(file.FileName),
            path = file.Path,
            size = file.Size,
            earliestname = Path.GetFileNameWithoutExtension(file.Video.EarliestKnownName),
            hashes = new HashesModel
            {
                crc = file.Video.Hashes.FirstOrDefault(h => h.Type is "CRC32")?.Value,
                md5 = file.Video.Hashes.FirstOrDefault(h => h.Type is "MD5")?.Value,
                ed2k = file.Video.ED2K,
                sha1 = file.Video.Hashes.FirstOrDefault(h => h.Type is "SHA1")?.Value,
            },
            anidb = AniDbToModel(file.Video.ReleaseInfo),
            media = MediaToModel(file.Video.MediaInfo),
            importfolder = ImportFolderToModel(file.ManagedFolder),
        };
    }

    public static ImportFolderModel ImportFolderToModel(IManagedFolder folder) => new()
    {
        id = folder.ID,
        name = folder.Name,
        location = folder.Path,
        type = folder.DropFolderType,
    };

    private static AniDbModel? AniDbToModel(IReleaseInfo? aniDb)
    {
        // Only AniDB-sourced releases (release URI under anidb.net/file/) map; the id is the URI tail.
        if (aniDb is not { ReleaseURI: var releaseUri } || !(releaseUri?.StartsWith("https://anidb.net/file/") ?? false))
            return null;
        return new AniDbModel
        {
            id = int.Parse(aniDb.ReleaseURI![23..]),
            censored = aniDb.IsCensored,
            source = Enum.GetName(aniDb.Source)!,
            version = aniDb.Version,
            releasedate = DateTimeToModel(aniDb.ReleasedAt?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)),
            description = aniDb.Comment,
            releasegroup = ReleaseGroupToModel(aniDb.Group),
            media = new AniDbMediaModel
            {
                sublanguages = (aniDb.MediaInfo?.SubtitleLanguages ?? []).ToList(),
                dublanguages = (aniDb.MediaInfo?.AudioLanguages ?? []).ToList(),
            },
        };
    }

    private static ReleaseGroupModel? ReleaseGroupToModel(IReleaseGroup? releaseGroup)
    {
        if (releaseGroup?.ID is null || releaseGroup.Name == "raw/unknown")
            return null;
        return new ReleaseGroupModel
        {
            name = releaseGroup.Name,
            shortname = releaseGroup.ShortName,
        };
    }

    private static MediaModel? MediaToModel(IMediaInfo? mediaInfo)
    {
        if (mediaInfo is null)
            return null;
        return new MediaModel
        {
            chaptered = mediaInfo.Chapters.Any(),
            duration = (long)mediaInfo.Duration.TotalSeconds,
            bitrate = mediaInfo.BitRate,
            sublanguages = mediaInfo.TextStreams.Select(s => s.Language.ToString()).ToList(),
            audio = mediaInfo.AudioStreams.Select(AudioToModel).ToList(),
            video = mediaInfo.VideoStream is { } video ? VideoToModel(video) : null,
        };
    }

    private static VideoModel VideoToModel(IVideoStream video) => new()
    {
        height = video.Height,
        width = video.Width,
        codec = video.Codec.Simplified,
        res = video.Resolution,
        bitrate = video.BitRate,
        bitdepth = video.BitDepth,
        framerate = (double)video.FrameRate,
    };

    private static AudioModel AudioToModel(IAudioStream audio) => new()
    {
        compressionmode = audio.CompressionMode,
        // a layout that includes an LFE channel is reported as e.g. 5.1 (5 full + 0.1 LFE).
        channels = !string.IsNullOrWhiteSpace(audio.ChannelLayout) && audio.ChannelLayout.Contains("LFE") ? audio.Channels - 1 + 0.1 : audio.Channels,
        samplingrate = audio.SamplingRate,
        codec = audio.Codec.Simplified,
        language = audio.Language.ToString(),
        title = audio.Title,
    };

    // ---- episode -------------------------------------------------------------------------------

    public static EpisodeModel EpisodeToModel(IAnidbEpisode episode, string prefix) => new()
    {
        getname = new TitleGetName(),
        duration = (long)episode.Runtime.TotalSeconds,
        number = episode.EpisodeNumber,
        type = episode.Type,
        airdate = DateTimeToModel(episode.AirDateWithTime),
        animeid = episode.SeriesID,
        id = episode.ID,
        titles = episode.Titles.OrderBy(t => t.Value).Select(TitleToModel).ToList(),
        prefix = prefix,
    };

    // ---- group ---------------------------------------------------------------------------------

    public static GroupModel GroupToModel(IShokoGroup group) => new()
    {
        name = string.IsNullOrWhiteSpace(group.PreferredTitle?.Value) ? null : group.PreferredTitle?.Value,
        // member anime keep their relations.
        mainanime = AnimeToModel(group.MainSeries.AnidbAnime),
        animes = group.AllSeries.Select(a => AnimeToModel(a.AnidbAnime)).ToList(),
    };

    // ---- tmdb ----------------------------------------------------------------------------------

    public static TmdbModel TmdbToModel(
        IEnumerable<ITmdbMovie> movies, IEnumerable<ITmdbShow> shows, IEnumerable<ITmdbEpisode> episodes) => new()
    {
        movies = movies.Select(MovieToModel).ToList(),
        shows = shows.Select(ShowToModel).ToList(),
        episodes = episodes.Select(TmdbEpisodeToModel).ToList(),
    };

    private static TmdbMovieModel MovieToModel(ITmdbMovie movie) => new()
    {
        getname = new TitleGetName(),
        id = movie.ID,
        titles = movie.Titles.Select(TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(movie.DefaultTitle?.Value) ? null : movie.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(movie.PreferredTitle?.Value) ? null : movie.PreferredTitle?.Value,
        rating = movie.Rating,
        restricted = movie.Restricted,
        studios = movie.Studios.Select(s => s.Name).ToList(),
        airdate = DateTimeToModel(movie.ReleaseDate),
    };

    private static TmdbShowModel ShowToModel(ITmdbShow show) => new()
    {
        getname = new TitleGetName(),
        id = show.ID,
        titles = show.Titles.Select(TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(show.DefaultTitle?.Value) ? null : show.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(show.PreferredTitle?.Value) ? null : show.PreferredTitle?.Value,
        rating = show.Rating,
        restricted = show.Restricted,
        studios = show.Studios.Select(st => st.Name).ToList(),
        episodecount = show.EpisodeCounts.Episodes,
        airdate = DateTimeToModel(show.AirDate?.ToDateTime()),
        enddate = DateTimeToModel(show.EndDate?.ToDateTime()),
        seasons = show.YearlySeasons.Select(SeasonToModel).ToList(),
    };

    private static TmdbEpisodeModel TmdbEpisodeToModel(ITmdbEpisode episode) => new()
    {
        getname = new TitleGetName(),
        showid = episode.SeriesID,
        id = episode.ID,
        titles = episode.Titles.Select(TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(episode.DefaultTitle?.Value) ? null : episode.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(episode.PreferredTitle?.Value) ? null : episode.PreferredTitle?.Value,
        type = episode.Type,
        number = episode.EpisodeNumber,
        seasonnumber = episode.SeasonNumber,
        airdate = DateTimeToModel(episode.AirDateWithTime),
    };
}
