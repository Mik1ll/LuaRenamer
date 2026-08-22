using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NSubstitute;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;

namespace LuaRenamer.Tests.Fakes;

/// <summary>
/// The one place the suite touches Shoko's metadata interfaces. Everything here returns a substitute that
/// stays mutable after construction, so a test that needs the standard graph with one value changed calls
/// <c>Returns</c> on that one member instead of rebuilding the arrangement.
/// </summary>
/// <remarks>
/// Members are stubbed explicitly rather than left to auto-substitution. Against a fast-moving prerelease
/// dependency that is the point: when a host interface moves, this file stops compiling at the member that
/// moved, instead of producing a null reference deep inside a producer.
/// </remarks>
public static class HostFakes
{
    public static ITitle Title(string value, TitleLanguage language = TitleLanguage.English, string code = "en",
        TitleType type = TitleType.Main, DataSource source = DataSource.AniDB)
    {
        ITitle title = Substitute.For<ITitle>();
        title.Value.Returns(value);
        title.Language.Returns(language);
        title.LanguageCode.Returns(code);
        title.Type.Returns(type);
        title.Source.Returns(source);
        return title;
    }

    public static IManagedFolder Folder(int id = Ids.Folder, string name = "testimport", string? path = null,
        DropFolderType type = DropFolderType.Destination)
    {
        IManagedFolder folder = Substitute.For<IManagedFolder>();
        folder.ID.Returns(id);
        folder.Name.Returns(name);
        folder.Path.Returns(path ?? Path.Combine("C:", "testimportfolder"));
        folder.DropFolderType.Returns(type);
        return folder;
    }

    public static IStudio Studio(string name)
    {
        IStudio studio = Substitute.For<IStudio>();
        studio.Name.Returns(name);
        return studio;
    }

    /// <summary>
    /// An AniDB anime with no Shoko series attached. <see cref="Series"/> wires the two-way link; a related
    /// anime reached through <see cref="Relation"/> is left unlinked, which is what makes its name fall back.
    /// </summary>
    public static IAnidbAnime Anime(int id = Ids.AnidbAnime, string title = "anidbTitle")
    {
        // Every substitute a Returns() argument needs is built first: constructing one inside the argument
        // configures *it* instead, leaving NSubstitute with no pending call to attach the return value to.
        ITitle defaultTitle = Title(title);
        IAnidbAnime anime = Substitute.For<IAnidbAnime>();
        anime.ID.Returns(id);
        anime.Title.Returns(title);
        anime.DefaultTitle.Returns(defaultTitle);
        anime.Titles.Returns([]);
        anime.RelatedSeries.Returns([]);
        anime.ShokoSeries.Returns([]);
        anime.Studios.Returns([]);
        anime.Tags.Returns([]);
        anime.YearlySeasons.Returns([]);
        anime.EpisodeCounts.Returns(new EpisodeCounts());
        anime.Type.Returns(AnimeType.TVSeries);
        anime.Rating.Returns(7.5);
        anime.Restricted.Returns(false);
        return anime;
    }

    public static IRelatedMetadata<ISeries, ISeries> Relation(IAnidbAnime related, RelationType type = RelationType.Sequel)
    {
        IRelatedMetadata<ISeries, ISeries> relation = Substitute.For<IRelatedMetadata<ISeries, ISeries>>();
        relation.Related.Returns(related);
        relation.RelationType.Returns(type);
        return relation;
    }

    /// <summary>
    /// A Shoko series and its AniDB anime, with the cycle between them wired in both directions. No test has
    /// to patch the back-reference itself.
    /// </summary>
    public static IShokoSeries Series(int shokoId = Ids.ShokoSeries, int anidbId = Ids.AnidbAnime,
        string shokoTitle = "shokoTitle", string anidbTitle = "anidbTitle", IAnidbAnime? anime = null)
    {
        anime ??= Anime(anidbId, anidbTitle);
        ITitle defaultTitle = Title(shokoTitle);
        IShokoSeries series = Substitute.For<IShokoSeries>();
        series.ID.Returns(shokoId);
        series.AnidbAnimeID.Returns(anidbId);
        series.AnidbAnime.Returns(anime);
        series.Title.Returns(shokoTitle);
        series.DefaultTitle.Returns(defaultTitle);
        series.Tags.Returns([]);
        series.Videos.Returns([]);
        series.TmdbMovies.Returns([]);
        series.TmdbShows.Returns([]);
        series.TmdbMovieCrossReferences.Returns([]);
        series.TmdbEpisodeCrossReferences.Returns([]);
        anime.ShokoSeries.Returns([series]);
        return series;
    }

    public static IAnidbEpisode AnidbEpisode(int id = Ids.AnidbEpisode, int anidbAnimeId = Ids.AnidbAnime,
        int number = 1, EpisodeType type = EpisodeType.Episode)
    {
        IAnidbEpisode episode = Substitute.For<IAnidbEpisode>();
        episode.ID.Returns(id);
        episode.SeriesID.Returns(anidbAnimeId);
        episode.EpisodeNumber.Returns(number);
        episode.Type.Returns(type);
        episode.Titles.Returns([]);
        episode.Runtime.Returns(TimeSpan.FromMinutes(24));
        episode.AirDateWithTime.Returns((DateTime?)null);
        return episode;
    }

    public static IShokoEpisode Episode(int shokoId = Ids.ShokoEpisode, int shokoSeriesId = Ids.ShokoSeries,
        IAnidbEpisode? anidbEpisode = null)
    {
        anidbEpisode ??= AnidbEpisode();
        IShokoEpisode episode = Substitute.For<IShokoEpisode>();
        episode.ID.Returns(shokoId);
        episode.SeriesID.Returns(shokoSeriesId);
        episode.AnidbEpisode.Returns(anidbEpisode);
        episode.AnidbEpisodeID.Returns(anidbEpisode.ID);
        episode.Type.Returns(anidbEpisode.Type);
        episode.EpisodeNumber.Returns(anidbEpisode.EpisodeNumber);
        episode.TmdbEpisodes.Returns([]);
        return episode;
    }

    public static IShokoGroup Group(int id, string name, IShokoSeries mainSeries, IReadOnlyList<IShokoSeries>? allSeries = null)
    {
        ITitle preferredTitle = Title(name);
        IShokoGroup group = Substitute.For<IShokoGroup>();
        group.ID.Returns(id);
        group.PreferredTitle.Returns(preferredTitle);
        group.MainSeriesID.Returns(mainSeries.ID);
        group.MainSeries.Returns(mainSeries);
        group.AllSeries.Returns(allSeries ?? [mainSeries]);
        return group;
    }

    public static IHashDigest Hash(string type, string value)
    {
        IHashDigest hash = Substitute.For<IHashDigest>();
        hash.Type.Returns(type);
        hash.Value.Returns(value);
        return hash;
    }

    public static IVideo Video(string ed2K = "ed2khash", IReleaseInfo? release = null, IMediaInfo? media = null)
    {
        IVideo video = Substitute.For<IVideo>();
        video.ID.Returns(Ids.Video);
        video.ED2K.Returns(ed2K);
        video.EarliestKnownName.Returns("earliest.mkv");
        video.Hashes.Returns([]);
        video.ReleaseInfo.Returns(release);
        video.MediaInfo.Returns(media);
        video.Files.Returns([]);
        return video;
    }

    public static IVideoFile File(IManagedFolder folder, IVideo video, string fileName = "testfilename.mp4",
        string? relativePath = null)
    {
        relativePath ??= Path.Combine("testsubfolder", fileName);
        IVideoFile file = Substitute.For<IVideoFile>();
        file.FileName.Returns(fileName);
        file.RelativePath.Returns(relativePath);
        file.Path.Returns(Path.Combine(folder.Path, relativePath));
        file.Size.Returns(123_456_789L);
        file.ManagedFolder.Returns(folder);
        file.ManagedFolderID.Returns(folder.ID);
        file.Video.Returns(video);
        file.VideoID.Returns(video.ID);
        return file;
    }

    public static IReleaseGroup ReleaseGroup(string id = "42", string name = "GoodGroup", string shortName = "GG")
    {
        IReleaseGroup group = Substitute.For<IReleaseGroup>();
        group.ID.Returns(id);
        group.Name.Returns(name);
        group.ShortName.Returns(shortName);
        return group;
    }

    /// <summary>The only URI shape <c>AniDbToModel</c> accepts; the id is the tail.</summary>
    public static readonly string AnidbReleaseUri = FormattableString.Invariant($"https://anidb.net/file/{Ids.AnidbFile}");

    public static IReleaseInfo Release(string? uri = null, IReleaseGroup? group = null)
    {
        uri ??= AnidbReleaseUri;
        IReleaseMediaInfo media = Substitute.For<IReleaseMediaInfo>();
        media.SubtitleLanguages.Returns([TitleLanguage.English]);
        media.AudioLanguages.Returns([TitleLanguage.Japanese]);

        IReleaseInfo release = Substitute.For<IReleaseInfo>();
        release.ReleaseURI.Returns(uri);
        release.IsCensored.Returns(false);
        release.Source.Returns(Enum.GetValues<ReleaseSource>().First());
        release.Version.Returns(2);
        release.ReleasedAt.Returns(new DateOnly(2021, 3, 4));
        release.Comment.Returns("release notes");
        release.Group.Returns(group);
        release.MediaInfo.Returns(media);
        return release;
    }

    public static IStreamCodecInfo Codec(string simplified)
    {
        IStreamCodecInfo codec = Substitute.For<IStreamCodecInfo>();
        codec.Simplified.Returns(simplified);
        return codec;
    }

    public static IAudioStream AudioStream(int channels = 6, string layout = "L R C LFE Ls Rs",
        TitleLanguage language = TitleLanguage.Japanese)
    {
        IStreamCodecInfo codec = Codec("AAC");
        IAudioStream audio = Substitute.For<IAudioStream>();
        audio.CompressionMode.Returns("Lossy");
        audio.Channels.Returns(channels);
        audio.ChannelLayout.Returns(layout);
        audio.SamplingRate.Returns(48000);
        audio.Codec.Returns(codec);
        audio.Language.Returns(language);
        audio.Title.Returns((string?)null);
        return audio;
    }

    public static IVideoStream VideoStream()
    {
        IStreamCodecInfo codec = Codec("h264");
        IVideoStream video = Substitute.For<IVideoStream>();
        video.Width.Returns(1920);
        video.Height.Returns(1080);
        video.Resolution.Returns("1080p");
        video.FrameRate.Returns(23.976m);
        video.BitRate.Returns(4_000_000);
        video.BitDepth.Returns(8);
        video.Codec.Returns(codec);
        return video;
    }

    public static IMediaInfo Media(IAudioStream? audio = null, IVideoStream? video = null)
    {
        audio ??= AudioStream();
        video ??= VideoStream();
        ITextStream text = Substitute.For<ITextStream>();
        text.Language.Returns(TitleLanguage.English);

        IMediaInfo media = Substitute.For<IMediaInfo>();
        media.Chapters.Returns([]);
        media.Duration.Returns(TimeSpan.FromMinutes(24));
        media.BitRate.Returns(5_000_000);
        media.TextStreams.Returns([text]);
        media.AudioStreams.Returns([audio]);
        media.VideoStream.Returns(video);
        return media;
    }

    public static ITmdbShow TmdbShow(int id = Ids.TmdbShow)
    {
        ITmdbShow show = Substitute.For<ITmdbShow>();
        show.ID.Returns(id);
        show.Titles.Returns([]);
        show.Studios.Returns([]);
        show.EpisodeCounts.Returns(new EpisodeCounts());
        show.YearlySeasons.Returns([]);
        return show;
    }

    public static ITmdbMovieCrossReference MovieCrossReference(int tmdbMovieId, int anidbEpisodeId)
    {
        ITmdbMovieCrossReference xref = Substitute.For<ITmdbMovieCrossReference>();
        xref.TmdbMovieID.Returns(tmdbMovieId);
        xref.AnidbEpisodeID.Returns(anidbEpisodeId);
        return xref;
    }

    // ---- focused arrangements -------------------------------------------------------------------
    //
    // Variations the producer tests need. They live here rather than in those tests so the host's metadata
    // interfaces stay named in one file: a test asks for "an anime with these titles" and asserts on the
    // model that comes out, never on the graph that went in.

    public static IAnidbAnime AnimeWithTitles(params (string Value, TitleLanguage Language, TitleType Type)[] titles)
    {
        ITitle[] built = [.. titles.Select(t => Title(t.Value, t.Language, "x", t.Type))];
        IAnidbAnime anime = Anime();
        anime.Titles.Returns(built);
        return anime;
    }

    public static IAnidbAnime AnimeWithSeasons(params (int Year, YearlySeason Season)[] seasons)
    {
        IAnidbAnime anime = Anime();
        anime.YearlySeasons.Returns(seasons);
        return anime;
    }

    /// <summary>
    /// An anime related to another one that points straight back at it. An unpruned recursion over this
    /// would not terminate, which is the case the relation coverage is about.
    /// </summary>
    public static IAnidbAnime AnimeInARelationCycle(string relatedName, RelationType type)
    {
        IAnidbAnime related = Anime(Ids.RelatedAnidbAnime, relatedName);
        IAnidbAnime anime = Anime();
        IRelatedMetadata<ISeries, ISeries> backwards = Relation(anime, RelationType.Prequel);
        related.RelatedSeries.Returns([backwards]);
        IRelatedMetadata<ISeries, ISeries> forwards = Relation(related, type);
        anime.RelatedSeries.Returns([forwards]);
        return anime;
    }

    public static IAnidbAnime AnimeRelatedToItself()
    {
        IAnidbAnime anime = Anime();
        IRelatedMetadata<ISeries, ISeries> toItself = Relation(anime);
        anime.RelatedSeries.Returns([toItself]);
        return anime;
    }

    /// <summary>A series carrying Shoko-side tags whose anime carries source-side ones, so the two are distinguishable.</summary>
    public static IShokoSeries SeriesWithTags(IReadOnlyList<string> shokoTags, IReadOnlyList<string> sourceTags)
    {
        IShokoTagForSeries[] custom = [.. shokoTags.Select(name =>
        {
            IShokoTagForSeries tag = Substitute.For<IShokoTagForSeries>();
            tag.Name.Returns(name);
            return tag;
        })];
        IAnidbTagForAnime[] source = [.. sourceTags.Select(name =>
        {
            IAnidbTagForAnime tag = Substitute.For<IAnidbTagForAnime>();
            tag.Name.Returns(name);
            return tag;
        })];

        IShokoSeries series = Series();
        series.Tags.Returns(custom);
        series.AnidbAnime.Tags.Returns(source);
        return series;
    }

    /// <summary>
    /// A video file carrying CRC32 and SHA1 digests but deliberately no MD5, so the per-type hash lookup has
    /// to come up empty for one of the three.
    /// </summary>
    public static IVideoFile FileWith(IReleaseInfo? release = null, IMediaInfo? media = null)
    {
        IHashDigest[] hashes = [Hash("CRC32", "CRCVAL"), Hash("SHA1", "SHA1VAL")];
        IManagedFolder folder = Folder();
        IVideo video = Video(release: release, media: media);
        video.Hashes.Returns(hashes);
        return File(folder, video, "My Video.mkv");
    }

    /// <summary>A video file whose AniDB release carries (or lacks) a release date.</summary>
    public static IVideoFile FileReleasedOn(DateOnly? released)
    {
        IReleaseInfo release = Release();
        release.ReleasedAt.Returns(released);
        IManagedFolder folder = Folder();
        IVideo video = Video(release: release);
        return File(folder, video);
    }
}
