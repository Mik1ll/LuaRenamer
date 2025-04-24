using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer;

public static partial class Utils
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

    private static readonly Dictionary<string, string> ReplaceMap = new()
    {
        { "/", "／" }, { "<", "＜" }, { ">", "＞" }, { ":", "：" }, { "\\", "＼" }, { "|", "｜" }, { "?", "？" }, { "*", "＊" }, { "\"", "＂" },
    };

    public static string NormPath(this string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

    public static string CleanPathSegment(this string segment, bool removeIllegalChars, bool replaceIllegalChars, bool platformDependentIllegalChars)
    {
        var windowsPathHandling = !platformDependentIllegalChars || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var illegalCharRegex = windowsPathHandling ? WindowsInvalidPathCharRegex() : OtherInvalidPathCharRegex();

        var newSegment = illegalCharRegex.Replace(segment, match => removeIllegalChars ? string.Empty :
                replaceIllegalChars ? ReplaceMap.GetValueOrDefault(match.Value, string.Empty) :
                "_")
            .TrimStart(' ').TrimEnd(' ', '.');
        var isEmpty = string.IsNullOrWhiteSpace(newSegment);
        if (isEmpty || (windowsPathHandling && WindowsDeviceNamesRegex().Match(newSegment).Success))
            throw new LuaRenamerException($"Illegal path segment: {(isEmpty ? "<empty/whitespace>" : newSegment)}");
        return newSegment;
    }

    [GeneratedRegex("""[<>:"/\\|?*\x00-\x1F]""", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsInvalidPathCharRegex();

    [GeneratedRegex("/")]
    private static partial Regex OtherInvalidPathCharRegex();

    [GeneratedRegex("""^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])(\..*)?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDeviceNamesRegex();
}
