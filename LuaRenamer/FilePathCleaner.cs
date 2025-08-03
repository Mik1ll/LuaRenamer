using System.Collections.Frozen;
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
    public static readonly FrozenDictionary<string, string> ReplaceMapDefaults = new Dictionary<string, string>
    {
        { "/", "／" }, { "<", "＜" }, { ">", "＞" }, { ":", "：" }, { "\\", "＼" }, { "|", "｜" }, { "?", "？" }, { "*", "＊" }, { "\"", "＂" },
    }.ToFrozenDictionary();

    private readonly Dictionary<string, string> _replaceMap = ReplaceMapDefaults.Concat(illegalCharsOverride).GroupBy(kvp => kvp.Key)
        .ToDictionary(g => g.Key, g => g.Last().Value);

    [GeneratedRegex("""[<>:"/\\|?*\x00-\x1F]""", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsInvalidPathCharRegex();

    [GeneratedRegex("""[/\0]""")]
    private static partial Regex OtherInvalidPathCharRegex();

    [GeneratedRegex("""^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])(\..*)?$""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDeviceNamesRegex();

    public string CleanPathSegment(string segment) => CleanPathSegments(segment)[0];

    public string[] CleanPathSegments(params string[] segments)
    {
        var windowsPathHandling = !platformDependentIllegalChars || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var illegalCharRegex = windowsPathHandling ? WindowsInvalidPathCharRegex() : OtherInvalidPathCharRegex();
        if (illegalCharRegex.Match(string.Join(null, _replaceMap.Values)) is { Success: true } m)
            throw new LuaRenamerException($"Illegal path replacement character: '{m.Value}'");

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
