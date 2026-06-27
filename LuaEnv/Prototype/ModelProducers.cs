using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLua;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;

namespace LuaRenamer.LuaEnv.Prototype;

/// <summary>
/// Leaf mappers shared by every producer (the model-architecture counterparts of LuaContext's
/// <c>TitleToTable</c> / <c>SeasonToTable</c> / <c>DateTimeToTable</c>).
/// </summary>
internal static class ProducerCommon
{
    public static TitleModel TitleToModel(ITitle title) => new()
    {
        name = title.Value,
        language = title.Language,
        languagecode = title.LanguageCode,
        type = title.Type,
    };

    public static SeasonModel SeasonToModel((int Year, YearlySeason Season) season) => new()
    {
        year = season.Year,
        season = season.Season,
    };

    public static DateTimeModel? DateTimeToModel(DateTime? dateTime)
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
}

/// <summary>
/// Builds the <see cref="FileModel"/> graph (file → media/audio/video, anidb → release-group, hashes,
/// import folder) from Shoko's <see cref="IVideoFile"/>. Field-by-field mirror of LuaContext's
/// <c>FileToTable</c>/<c>MediaInfoToTable</c>/<c>AniDbFileToTable</c>/etc., producing decoupled models.
/// </summary>
public static class FileModelProducer
{
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
            releasedate = ProducerCommon.DateTimeToModel(aniDb.ReleasedAt?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)),
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
}

/// <summary>
/// Builds <see cref="EpisodeModel"/> from Shoko's <see cref="IAnidbEpisode"/>. Mirror of LuaContext's
/// <c>EpisodeToTable</c>. <paramref name="getname"/> (the shared <c>_getName</c> Lua function) and
/// <paramref name="prefix"/> (host's <c>Utils.EpPrefix[type]</c>) are injected, keeping the producer
/// free of host wiring.
/// </summary>
public static class EpisodeModelProducer
{
    public static EpisodeModel EpisodeToModel(IAnidbEpisode episode, LuaFunction getname, string prefix) => new()
    {
        getname = getname,
        duration = (long)episode.Runtime.TotalSeconds,
        number = episode.EpisodeNumber,
        type = episode.Type,
        airdate = ProducerCommon.DateTimeToModel(episode.AirDateWithTime),
        animeid = episode.SeriesID,
        id = episode.ID,
        titles = episode.Titles.OrderBy(t => t.Value).Select(ProducerCommon.TitleToModel).ToList(),
        prefix = prefix,
    };
}

/// <summary>
/// Builds <see cref="GroupModel"/> from Shoko's <see cref="IShokoGroup"/>. Mirror of LuaContext's
/// <c>GroupToTable</c>; member anime keep their relations (LuaContext passes <c>ignoreRelations: false</c>).
/// </summary>
public static class GroupModelProducer
{
    public static GroupModel GroupToModel(IShokoGroup group, LuaFunction getname) => new()
    {
        name = string.IsNullOrWhiteSpace(group.PreferredTitle?.Value) ? null : group.PreferredTitle?.Value,
        mainanime = AnimeModelProducer.AnimeToModel(group.MainSeries.AnidbAnime, getname),
        animes = group.AllSeries.Select(a => AnimeModelProducer.AnimeToModel(a.AnidbAnime, getname)).ToList(),
    };
}

/// <summary>
/// Builds <see cref="TmdbModel"/> from already-gathered TMDB collections (the host pulls these off the
/// relocation context). Mirror of LuaContext's <c>TmdbToTable</c> and its per-entity helpers.
/// </summary>
public static class TmdbModelProducer
{
    public static TmdbModel TmdbToModel(
        IEnumerable<ITmdbMovie> movies, IEnumerable<ITmdbShow> shows, IEnumerable<ITmdbEpisode> episodes, LuaFunction getname) => new()
    {
        movies = movies.Select(m => MovieToModel(m, getname)).ToList(),
        shows = shows.Select(s => ShowToModel(s, getname)).ToList(),
        episodes = episodes.Select(e => EpisodeToModel(e, getname)).ToList(),
    };

    private static TmdbMovieModel MovieToModel(ITmdbMovie movie, LuaFunction getname) => new()
    {
        getname = getname,
        id = movie.ID,
        titles = movie.Titles.Select(ProducerCommon.TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(movie.DefaultTitle?.Value) ? null : movie.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(movie.PreferredTitle?.Value) ? null : movie.PreferredTitle?.Value,
        rating = movie.Rating,
        restricted = movie.Restricted,
        studios = movie.Studios.Select(s => s.Name).ToList(),
        airdate = ProducerCommon.DateTimeToModel(movie.ReleaseDate),
    };

    private static TmdbShowModel ShowToModel(ITmdbShow show, LuaFunction getname) => new()
    {
        getname = getname,
        id = show.ID,
        titles = show.Titles.Select(ProducerCommon.TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(show.DefaultTitle?.Value) ? null : show.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(show.PreferredTitle?.Value) ? null : show.PreferredTitle?.Value,
        rating = show.Rating,
        restricted = show.Restricted,
        studios = show.Studios.Select(st => st.Name).ToList(),
        episodecount = show.EpisodeCounts.Episodes,
        airdate = ProducerCommon.DateTimeToModel(show.AirDate?.ToDateTime()),
        enddate = ProducerCommon.DateTimeToModel(show.EndDate?.ToDateTime()),
        seasons = show.YearlySeasons.Select(ProducerCommon.SeasonToModel).ToList(),
    };

    private static TmdbEpisodeModel EpisodeToModel(ITmdbEpisode episode, LuaFunction getname) => new()
    {
        getname = getname,
        showid = episode.SeriesID,
        id = episode.ID,
        titles = episode.Titles.Select(ProducerCommon.TitleToModel).ToList(),
        defaultname = string.IsNullOrWhiteSpace(episode.DefaultTitle?.Value) ? null : episode.DefaultTitle?.Value,
        preferredname = string.IsNullOrWhiteSpace(episode.PreferredTitle?.Value) ? null : episode.PreferredTitle?.Value,
        type = episode.Type,
        number = episode.EpisodeNumber,
        seasonnumber = episode.SeasonNumber,
        airdate = ProducerCommon.DateTimeToModel(episode.AirDateWithTime),
    };
}
