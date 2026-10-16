namespace LuaRenamer.Tests.Fakes;

/// <summary>
/// One band per identifier space, disjoint and never zero — an arrangement convention, not a property of the
/// system: real Shoko and AniDB ids are independent sequences and may coincide. Production code compares
/// identifiers across spaces in several places (a group's main-series id is a Shoko id, an episode's series id
/// is an AniDB id); drawing each space from its own band keeps such a mix-up from matching by coincidence in a
/// test, so it shows up as a failure rather than as an accidental pass.
/// </summary>
public static class Ids
{
    public const int Folder = 11;
    public const int SecondFolder = 12;
    public const int ThirdFolder = 13;

    public const int ShokoSeries = 101;
    public const int OtherShokoSeries = 102;

    public const int ShokoEpisode = 201;
    public const int OtherShokoEpisode = 202;

    public const int ShokoGroup = 301;
    public const int OtherShokoGroup = 302;

    public const int AnidbAnime = 1001;
    public const int OtherAnidbAnime = 1002;
    public const int RelatedAnidbAnime = 1003;

    public const int AnidbEpisode = 2001;
    public const int OtherAnidbEpisode = 2002;

    public const int TmdbShow = 50001;
    public const int TmdbMovie = 50002;
    public const int TmdbEpisode = 50003;

    public const int AnidbFile = 900001;
    public const int Video = 700001;
}
