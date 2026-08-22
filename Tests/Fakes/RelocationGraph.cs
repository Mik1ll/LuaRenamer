using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NSubstitute;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Relocation;

namespace LuaRenamer.Tests.Fakes;

/// <summary>
/// A complete, wired relocation graph: one import folder, one series and its anime, one episode, one file.
/// Enough to drive <see cref="LuaRenamer.GetPath"/> to completion, and every piece is reachable and mutable,
/// so a test varies exactly what it cares about.
/// </summary>
/// <remarks>
/// The pieces are substitutes, so a single-value change is <c>graph.Anime.Type.Returns(AnimeType.Movie)</c>
/// rather than a rebuilt graph. The collections are mutable lists for the same reason — appending a second
/// import folder or a second series restates nothing.
/// </remarks>
public sealed class RelocationGraph
{
    private RelocationGraph(IManagedFolder folder, IShokoSeries series, IShokoEpisode episode, IVideo video, IVideoFile file)
    {
        Folder = folder;
        Series = series;
        Episode = episode;
        Video = video;
        File = file;
        Folders = [folder];
        SeriesList = [series];
        Episodes = [episode];
        Groups = [];
    }

    public static RelocationGraph Default()
    {
        IManagedFolder folder = HostFakes.Folder();
        IShokoSeries series = HostFakes.Series();
        IShokoEpisode episode = HostFakes.Episode();
        IVideo video = HostFakes.Video();
        return new RelocationGraph(folder, series, episode, video, HostFakes.File(folder, video));
    }

    /// <summary>
    /// The default graph with every optional slice filled in — media with a video and audio stream, an AniDB
    /// release with a group, titles on the anime and the episode, a season, a TMDB show and a group. What the
    /// shipped default script needs, and the arrangement an end-to-end test would otherwise assemble by hand.
    /// </summary>
    public static RelocationGraph Populated()
    {
        IMediaInfo media = HostFakes.Media();
        IReleaseGroup releaseGroup = HostFakes.ReleaseGroup();
        IReleaseInfo release = HostFakes.Release(group: releaseGroup);
        IHashDigest[] hashes = [HostFakes.Hash("CRC32", "ABCD1234")];

        IManagedFolder folder = HostFakes.Folder();
        IShokoSeries series = HostFakes.Series();
        IVideo video = HostFakes.Video(release: release, media: media);
        video.Hashes.Returns(hashes);
        IShokoEpisode episode = HostFakes.Episode();

        ITitle[] animeTitles = [HostFakes.Title("Populated Anime"), HostFakes.Title("Populated Anime JP", TitleLanguage.Japanese, "ja")];
        series.AnidbAnime.Titles.Returns(animeTitles);
        series.AnidbAnime.YearlySeasons.Returns([(2024, YearlySeason.Winter)]);
        IStudio[] studios = [HostFakes.Studio("Studio A")];
        series.AnidbAnime.Studios.Returns(studios);

        ITitle[] episodeTitles = [HostFakes.Title("Populated Episode")];
        episode.AnidbEpisode.Titles.Returns(episodeTitles);

        ITmdbShow[] shows = [HostFakes.TmdbShow()];
        series.TmdbShows.Returns(shows);

        var graph = new RelocationGraph(folder, series, episode, video, HostFakes.File(folder, video));
        graph.Groups.Add(HostFakes.Group(Ids.ShokoGroup, "Populated Group", series));
        return graph;
    }

    public IManagedFolder Folder { get; }

    public IShokoSeries Series { get; }

    /// <summary>The series' AniDB anime — the same instance the series points at, cycle already wired.</summary>
    public IAnidbAnime Anime => Series.AnidbAnime;

    public IShokoEpisode Episode { get; }

    public IAnidbEpisode AnidbEpisode => Episode.AnidbEpisode;

    public IVideo Video { get; }

    public IVideoFile File { get; }

    public List<IManagedFolder> Folders { get; }

    public List<IShokoSeries> SeriesList { get; }

    public List<IShokoEpisode> Episodes { get; }

    public List<IShokoGroup> Groups { get; }

    public bool MoveEnabled { get; set; } = true;

    public bool RenameEnabled { get; set; } = true;

    public LuaRenamerSettings Settings { get; } = new();

    public RelocationContext<LuaRenamerSettings> Context(string script)
    {
        Settings.Script = script;
        return new RelocationContext<LuaRenamerSettings>(new RelocationContext
        {
            CancellationToken = CancellationToken.None,
            AvailableFolders = Folders,
            File = File,
            Episodes = Episodes,
            Series = SeriesList,
            Groups = Groups,
            MoveEnabled = MoveEnabled,
            RenameEnabled = RenameEnabled,
        }, Settings);
    }

    // ---- focused arrangements -------------------------------------------------------------------

    /// <summary>
    /// <paramref name="order"/>.Count series whose Shoko and source ids both ascend, added to the context in
    /// the given order — so index 0 is always the primary and never necessarily first in the list.
    /// </summary>
    /// <returns>The graph, plus each series' source id and Shoko title indexed by rank, not by arrival order.</returns>
    public static (RelocationGraph Graph, IReadOnlyList<int> SourceIds, IReadOnlyList<string> Titles) MultiSeries(
        IReadOnlyList<int> order, bool withGroups = false)
    {
        RelocationGraph graph = Default();
        List<IShokoSeries> series =
        [
            .. Enumerable.Range(0, order.Count)
                .Select(i => HostFakes.Series(Ids.ShokoSeries + i, Ids.AnidbAnime + i, $"shoko{i}", $"anidb{i}")),
        ];

        graph.SeriesList.Clear();
        graph.SeriesList.AddRange(order.Select(i => series[i]));
        graph.Episodes.Clear();
        graph.Episodes.AddRange(order.Select(i =>
            HostFakes.Episode(Ids.ShokoEpisode + i, series[i].ID,
                HostFakes.AnidbEpisode(Ids.AnidbEpisode + i, series[i].AnidbAnimeID))));
        if (withGroups)
            graph.Groups.AddRange(order.Select(i => HostFakes.Group(Ids.ShokoGroup + i, $"group{i}", series[i])));

        return (graph, [.. series.Select(s => s.AnidbAnimeID)], [.. series.Select(s => s.Title)]);
    }

    /// <summary>Two series where only the primary one is linked to a TMDB show, listed primary-last.</summary>
    public static RelocationGraph TwoSeriesWithTmdbOnThePrimaryOne()
    {
        ITmdbShow[] shows = [HostFakes.TmdbShow()];
        IShokoSeries primary = HostFakes.Series(Ids.ShokoSeries, Ids.AnidbAnime);
        primary.TmdbShows.Returns(shows);
        IShokoSeries other = HostFakes.Series(Ids.OtherShokoSeries, Ids.OtherAnidbAnime);

        RelocationGraph graph = Default();
        graph.SeriesList.Clear();
        graph.SeriesList.AddRange([other, primary]);
        graph.Episodes.Clear();
        graph.Episodes.Add(HostFakes.Episode(Ids.ShokoEpisode, primary.ID,
            HostFakes.AnidbEpisode(Ids.AnidbEpisode, primary.AnidbAnimeID)));
        return graph;
    }

    /// <summary>Replaces the graph's episodes, all belonging to the primary series.</summary>
    public void SetEpisodes(IReadOnlyList<(int Number, EpisodeType Type)> episodes)
    {
        Episodes.Clear();
        Episodes.AddRange(episodes.Select((e, i) =>
            HostFakes.Episode(Ids.ShokoEpisode + i, Series.ID,
                HostFakes.AnidbEpisode(Ids.AnidbEpisode + i, Series.AnidbAnimeID, e.Number, e.Type))));
    }

    /// <summary>
    /// Replaces the graph's series and episodes, where <c>Series</c> indexes 1..n into a set of series whose
    /// ids ascend — series 1 being the primary. Episodes of the others exist only to be filtered out.
    /// </summary>
    public void SetSeriesAndEpisodes(int seriesCount, IReadOnlyList<(int Series, int Number, EpisodeType Type)> episodes)
    {
        List<IShokoSeries> series =
        [
            Series,
            .. Enumerable.Range(1, seriesCount - 1)
                .Select(i => HostFakes.Series(Ids.OtherShokoSeries + i, Ids.OtherAnidbAnime + i)),
        ];
        SeriesList.Clear();
        SeriesList.AddRange(series);

        Episodes.Clear();
        Episodes.AddRange(episodes.Select((e, i) =>
            HostFakes.Episode(Ids.ShokoEpisode + i, series[e.Series - 1].ID,
                HostFakes.AnidbEpisode(Ids.AnidbEpisode + i, series[e.Series - 1].AnidbAnimeID, e.Number, e.Type))));
    }

    /// <summary>Adds a second import folder and returns its id.</summary>
    public int AddFolder(string name, string path, DropFolderType type = DropFolderType.Destination)
    {
        IManagedFolder folder = HostFakes.Folder(Ids.Folder + Folders.Count, name, path, type);
        Folders.Add(folder);
        return folder.ID;
    }

    /// <summary>The path of the import folder with the given id, as the host reports it.</summary>
    public string FolderPath(int id) => Folders.Single(f => f.ID == id).Path;

    /// <summary>
    /// Gives the primary series files that already live somewhere, each described by its own hash, the
    /// import folder it sits in, and the subfolder within it (empty meaning the folder root).
    /// </summary>
    public void GiveSeriesExistingFiles(params (string Ed2K, int FolderID, string Subfolder)[] existing)
    {
        IVideo[] videos =
        [
            .. existing.Select(e =>
            {
                IManagedFolder folder = Folders.Single(f => f.ID == e.FolderID);
                IVideo video = HostFakes.Video(e.Ed2K);
                IVideoFile[] files = [HostFakes.File(folder, video, "existing.mkv", Path.Combine(e.Subfolder, "existing.mkv"))];
                video.Files.Returns(files);
                return video;
            }),
        ];
        Series.Videos.Returns(videos);
    }

    /// <summary>The hash of the file being relocated — the one an existing location must not be taken from.</summary>
    public string FileHash => Video.ED2K;

    /// <summary>
    /// Leaves the Shoko-side series title blank, so anything reading it has to fall back to the source
    /// metadata title rather than pass the blank along.
    /// </summary>
    public void BlankOutTheSeriesTitle() => Series.Title.Returns("   ");
}
