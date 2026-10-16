using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shoko.Abstractions.Metadata;
using Xunit;

namespace LuaRenamer.Tests.Env;

public class TmdbProducerTests
{
    private static TmdbModel Tmdb(RelocationGraph graph) =>
        ModelProducers.EnvToModel(graph.Context(""), new FakeLogger<LuaRenamer>()).tmdb;

    [Fact]
    public void AMovieCarriesTheAnidbEpisodesItsCrossReferencesName()
    {
        var graph = RelocationGraph.Default();
        graph.LinkToTmdb();

        TmdbMovieModel movie = Tmdb(graph).movies.Should().ContainSingle().Subject;

        movie.id.Should().Be(Ids.TmdbMovie);
        movie.anidbepisodeids.Should().Equal(Ids.AnidbEpisode);
    }

    [Fact]
    public void AnEpisodeCarriesTheAnidbEpisodesItsCrossReferencesName()
    {
        var graph = RelocationGraph.Default();
        graph.LinkToTmdb();

        TmdbEpisodeModel episode = Tmdb(graph).episodes.Should().ContainSingle().Subject;

        episode.id.Should().Be(Ids.TmdbEpisode);
        episode.showid.Should().Be(Ids.TmdbShow);
        episode.anidbepisodeids.Should().Equal(Ids.AnidbEpisode);
    }

    [Fact]
    public void TheAnidbAnimeIsNotListedAsATmdbShow() =>
        Tmdb(RelocationGraph.Default()).shows.Should().BeEmpty();

    [Fact]
    public void TheAnidbEpisodeIsNotListedAsATmdbEpisode() =>
        Tmdb(RelocationGraph.Default()).episodes.Should().BeEmpty();

    [Fact]
    public void ATmdbEpisodeCoveringSeveralAnidbEpisodesIsListedOnceWithAllOfThem()
    {
        var graph = RelocationGraph.Default();
        graph.LinkTwoEpisodesToOneTmdbEpisode();

        TmdbEpisodeModel episode = Tmdb(graph).episodes.Should().ContainSingle().Subject;

        episode.id.Should().Be(Ids.TmdbEpisode);
        episode.anidbepisodeids.Should().Equal(Ids.AnidbEpisode, Ids.AnidbEpisode + 1);
    }

    [Fact]
    public void WithNoTitleInAPreferredLanguageOnlyTheDefaultNameIsSet()
    {
        var graph = RelocationGraph.Default();
        graph.LinkToTmdb();
        graph.Series.LinkedMovies[0].PreferredTitle.Returns((ITitle?)null);

        TmdbMovieModel movie = Tmdb(graph).movies.Should().ContainSingle().Subject;

        movie.preferredname.Should().BeNull();
        movie.defaultname.Should().Be("tmdbMovie");
    }
}
