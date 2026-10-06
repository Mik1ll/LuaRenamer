using System.Globalization;
using Shoko.Abstractions.Metadata;

namespace LuaRenamer.Tests;

internal static class TestGuids
{
    internal static MetadataGuid AnidbSeriesGuid(int anidbId) =>
        new(MetadataSource.AniDB, MetadataEntityType.Series, anidbId.ToString(CultureInfo.InvariantCulture));
}
