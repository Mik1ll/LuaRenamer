using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LuaRenamer;

public partial class FilePathCleaner(
    bool removeIllegalChars,
    bool replaceIllegalChars,
    bool platformDependentIllegalChars,
    Dictionary<string, string> illegalCharsOverride)
{
    private readonly Dictionary<string, string> _replaceMap = new Dictionary<string, string>
        {
            { "/", "／" }, { "<", "＜" }, { ">", "＞" }, { ":", "：" }, { "\\", "＼" }, { "|", "｜" }, { "?", "？" }, { "*", "＊" }, { "\"", "＂" },
        }.Concat(illegalCharsOverride).GroupBy(kvp => kvp.Key)
        .ToDictionary(g => g.Key, g => g.Last().Value);

    [GeneratedRegex("""[<>:"/\\|?*\x00-\x1F]""", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsInvalidPathCharRegex();

    [GeneratedRegex("/")]
    private static partial Regex OtherInvalidPathCharRegex();

    [GeneratedRegex("""^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])(\..*)?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDeviceNamesRegex();

    public string CleanPathSegment(string segment) => CleanPathSegments(segment)[0];

    public string[] CleanPathSegments(params string[] segments)
    {
        var windowsPathHandling = !platformDependentIllegalChars || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var illegalCharRegex = windowsPathHandling ? WindowsInvalidPathCharRegex() : OtherInvalidPathCharRegex();

        var newSegments = new List<string>();
        foreach (var segment in segments)
        {
            var newSegment = illegalCharRegex.Replace(segment, match => removeIllegalChars ? string.Empty :
                    replaceIllegalChars ? _replaceMap.GetValueOrDefault(match.Value, string.Empty) :
                    "_")
                .TrimStart(' ').TrimEnd(' ', '.');
            var isEmpty = string.IsNullOrWhiteSpace(newSegment);
            if (isEmpty || (windowsPathHandling && WindowsDeviceNamesRegex().Match(newSegment).Success))
                throw new LuaRenamerException($"Illegal path segment: {(isEmpty ? "<empty/whitespace>" : newSegment)}");
            newSegments.Add(newSegment);
        }

        return newSegments.ToArray();
    }
}
