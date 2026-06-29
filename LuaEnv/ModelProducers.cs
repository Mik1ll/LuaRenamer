using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Maps Shoko's host abstractions to the plain <see cref="ILuaModel"/> graph that <see cref="LuaSerializer"/>
/// materializes. The model-architecture counterpart of LuaContext's old <c>*ToTable</c> methods: the same
/// field-by-field mappings, but producing decoupled models instead of mutating a live LuaTable, and with no
/// reference-dedup cache (intentionally dropped — the graph terminates because nested relation anime are
/// built with <c>includeRelations: false</c>).
/// </summary>
/// <remarks>
/// <c>getname</c> is a pure <see cref="AnimeGetName"/>/<see cref="TitleGetName"/> descriptor of the shared
/// title-resolver closure; <see cref="LuaSerializer"/> mints the live Lua handle from it, so the producers
/// carry no host wiring. <c>prefix</c> on episodes is the host's <c>Utils.EpPrefix[type]</c>, injected for the same reason.
/// </remarks>
public static class ModelProducers
{
    // ---- env root ------------------------------------------------------------------------------

    /// <summary>
    /// Builds the whole <see cref="EnvModel"/> graph from the relocation <paramref name="args"/>. The data
    /// comes off the non-generic <see cref="RelocationContext"/> base (so LuaEnv stays ignorant of the host's
    /// settings type); the host policy it can't derive — the illegal-char config, the default replacement map,
    /// and the host-bound <c>episode_numbers</c>/<c>log*</c> delegates — is passed in.
    /// </summary>
    public static EnvModel EnvToModel(
        RelocationContext args,
        IShokoSeries primarySeries,
        IShokoEpisode primaryEpisode,
        IReadOnlyDictionary<EpisodeType, string> epPrefix,
        bool replaceIllegalChars,
        bool removeIllegalChars,
        bool useExistingAnimeLocation,
        IReadOnlyDictionary<string, string> illegalCharsMap,
        EpisodeNumbersDelegate episodeNumbers,
        LogDelegate logdebug,
        LogDelegate log,
        LogDelegate logwarn,
        LogDelegate logerror)
    {
        var animes = args.Series
            .OrderBy(s => s.AnidbAnimeID != primarySeries.AnidbAnimeID)
            .ThenBy(s => s.AnidbAnimeID)
            .Select(series => AnimeToModel(series.AnidbAnime)).ToList();
        var episodes = args.Episodes
            .OrderBy(e => e.AnidbEpisodeID != primaryEpisode.AnidbEpisodeID)
            .ThenBy(e => e.AnidbEpisode.SeriesID)
            .ThenBy(e => e.AnidbEpisode.Type == EpisodeType.Other ? int.MinValue : (int)e.AnidbEpisode.Type)
            .ThenBy(e => e.AnidbEpisode.EpisodeNumber)
            .Select(e => EpisodeToModel(e.AnidbEpisode, epPrefix[e.AnidbEpisode.Type])).ToList();
        var groups = args.Groups
            .OrderBy(g => g.MainSeriesID != primarySeries.AnidbAnimeID)
            .Select(GroupToModel).ToList();

        return new EnvModel
        {
            episode_numbers = episodeNumbers,
            logdebug = logdebug,
            log = log,
            logwarn = logwarn,
            logerror = logerror,
            replace_illegal_chars = replaceIllegalChars,
            remove_illegal_chars = removeIllegalChars,
            use_existing_anime_location = useExistingAnimeLocation,
            skip_rename = false,
            skip_move = false,
            illegal_chars_map = illegalCharsMap,
            animes = animes,
            anime = animes[0],
            file = FileToModel(args.File),
            episodes = episodes,
            episode = episodes[0],
            importfolders = args.AvailableFolders.Select(ImportFolderToModel).ToList(),
            groups = groups,
            group = groups.Count > 0 ? groups[0] : null,
            tmdb = TmdbToModel(
                args.Series[0].TmdbMovies,
                args.Series[0].TmdbShows,
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
    /// Builds the identity name→name map for an enum (counterpart of <c>LuaContext.EnumToTable&lt;T&gt;</c>).
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
            preferredname = string.IsNullOrWhiteSpace(series?.Title) ? anime.Title : series.Title,
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
        // nested anime gets includeRelations: false (mirrors AnimeToTable's ignoreRelations) so the
        // graph terminates without the cache the old code relied on.
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
        // member anime keep their relations (LuaContext passed ignoreRelations: false).
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
