using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Enums;

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

    public static string NormPath(this string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
}
