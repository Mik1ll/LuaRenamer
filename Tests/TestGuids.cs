using System.Globalization;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;

namespace LuaRenamer.Tests;

internal static class TestGuids
{
    internal static MetadataGuid AnidbSeriesGuid(int anidbId) =>
        new(MetadataSource.AniDB, MetadataEntityType.Series, anidbId.ToString(CultureInfo.InvariantCulture));

    // The env model reads anime.id from AnidbID but compares relations by ID, so the two must never disagree.
    internal static void SetupAnidbId(Mock<IAnidbAnime> mock, int anidbId)
    {
        _ = mock.SetupGet(a => a.ID).Returns(AnidbSeriesGuid(anidbId));
        _ = mock.SetupGet(a => a.AnidbID).Returns(anidbId);
    }
}
