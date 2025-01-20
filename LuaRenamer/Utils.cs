using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer;

public static class Utils
{
    public static string NormPath(this string path) => path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);

    private static readonly Dictionary<string, string> ReplaceMap = new()
    {
        { "/", "／" }, { "<", "＜" }, { ">", "＞" }, { ":", "：" }, { "\\", "＼" }, { "|", "｜" }, { "?", "？" }, { "*", "＊" }, { "\"", "＂" }
    };

    private static readonly Regex InvalidPathCharRegex = new(@"[<>:""/\\|?*\x00-\x1F]", RegexOptions.Compiled);

    private static readonly Regex WindowsDeviceNamesRegex = new("^(CON|PRN|AUX|NUL|COM[0-9\xB9\xB2\xB3]|LPT[0-9\xB9\xB2\xB3])(\\..+)?$", RegexOptions.Compiled);

    public static string CleanPathSegment(this string segment, bool removeIllegalChars, bool replaceIllegalChars)
    {
        segment = InvalidPathCharRegex.Replace(segment, match => removeIllegalChars
            ? string.Empty
            : replaceIllegalChars
                ? ReplaceMap.GetValueOrDefault(match.Value, string.Empty)
                : "_").TrimStart(' ').TrimEnd(' ', '.');
        var isEmpty = string.IsNullOrWhiteSpace(segment);
        if (isEmpty || WindowsDeviceNamesRegex.Match(segment).Success)
            throw new LuaRenamerException($"Illegal path segment: {(isEmpty ? "<empty/whitespace>" : segment)}");
        return segment;
    }

    public static readonly Dictionary<EpisodeType, string> EpPrefix = new()
    {
        { EpisodeType.Episode, "" },
        { EpisodeType.Special, "S" },
        { EpisodeType.Credits, "C" },
        { EpisodeType.Other, "O" },
        { EpisodeType.Parody, "P" },
        { EpisodeType.Trailer, "T" }
    };
}
