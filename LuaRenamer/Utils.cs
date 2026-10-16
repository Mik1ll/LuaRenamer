using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer;

public static class Utils
{
    public static readonly Dictionary<EpisodeType, string> EpPrefix = new()
    {
        { EpisodeType.Episode, "" },
        { EpisodeType.Special, "S" },
        { EpisodeType.Credits, "C" },
        { EpisodeType.Other, "O" },
        { EpisodeType.Parody, "P" },
        { EpisodeType.Trailer, "T" },
    };

    public static string NormPath(this string path)
    {
        path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        // TrimEndingDirectorySeparator removes only one separator per call, but still won't trim into the root
        for (var trimmed = Path.TrimEndingDirectorySeparator(path); trimmed != path; trimmed = Path.TrimEndingDirectorySeparator(path))
            path = trimmed;
        return path;
    }
}
