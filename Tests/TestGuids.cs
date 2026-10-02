using System.Globalization;
using Shoko.Abstractions.Metadata;

namespace LuaRenamer.Tests;

/// <summary>
/// Builds the metadata IDs the mocks return.
/// </summary>
internal static class TestGuids
{
    /// <summary>
    /// The ID of the AniDB anime <paramref name="anidbId"/>.
    /// </summary>
    internal static MetadataGuid AnidbSeriesGuid(int anidbId) =>
        new(MetadataSource.AniDB, MetadataEntityType.Series, anidbId.ToString(CultureInfo.InvariantCulture));
}
