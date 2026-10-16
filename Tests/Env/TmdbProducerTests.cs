using AwesomeAssertions;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
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
}
