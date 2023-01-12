using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer;

public static class Utils
{
    public static string NormPath(this string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
    }

    private static readonly Dictionary<string, string> ReplaceMap = new()
    {
        { "/", "／" }, { "<", "＜" }, { ">", "＞" }, { ":", "：" }, { "\\", "＼" }, { "|", "｜" }, { "?", "？" }, { "*", "＊" }, { "\"", "＂" }
    };

    private static readonly Regex ReplacePathSegmentCharsCompiledRegex = new(@"[<>:""/\\|?*]", RegexOptions.Compiled);

    public static string ReplacePathSegmentChars(this string segment, bool withAlternate)
    {
        return ReplacePathSegmentCharsCompiledRegex.Replace(segment, match => withAlternate ? ReplaceMap[match.Value] : "_");
    }

    private static readonly Regex CleanPathSegmentRegex1 = new(@"[<>:""/\\|?*\x00-\x1F]+", RegexOptions.Compiled);
    private static readonly Regex CleanPathSegmentRegex2 = new(@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.Compiled);

    public static string CleanPathSegment(this string segment, bool isFilename)
    {
        segment = CleanPathSegmentRegex1.Replace(segment, string.Empty).Trim();
        if (!isFilename)
            segment = segment.TrimEnd(' ', '.');
        var isEmpty = string.IsNullOrWhiteSpace(segment);
        if (isEmpty || CleanPathSegmentRegex2.Match(segment).Success)
            throw new ArgumentException($"Illegal path segment: {(isEmpty ? "<empty/whitespace>" : segment)}");
        return segment;
    }

    public static T? ParseEnum<T>(string text, bool throwException = true)
    {
        try
        {
            return (T)Enum.Parse(typeof(T), string.Concat(text.Where(c => !char.IsWhiteSpace(c))), true);
        }
        catch
        {
            if (throwException)
                throw;
            return default;
        }
    }

    public static Type? GetTypeFromAssemblies(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().Select(currentassembly => currentassembly.GetType(typeName, false, true))
            .FirstOrDefault(t => t is not null);
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
