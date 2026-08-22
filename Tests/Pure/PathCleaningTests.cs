using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace LuaRenamer.Tests.Pure;

/// <summary>
/// The path-cleaning contract, stated as invariants over generated input rather than as enumerated rows.
/// Nothing here constructs a Lua state or a host metadata graph — cleaning is a pure string transform.
/// </summary>
public class PathCleaningTests
{
    /// <summary>
    /// The platform's own rules, written out independently of the regexes under test so the properties are a
    /// check rather than a restatement: Windows forbids these nine characters plus codepoints 0-31, and
    /// reserves these device names; everywhere else only the separator and NUL are forbidden.
    /// </summary>
    private static class Platform
    {
        private const string WindowsIllegal = """<>:"/\|?*""";

        // The superscript forms are here because Windows itself resolves COM¹ to COM1.
        private static readonly string[] ReservedStems = ["CON", "PRN", "AUX", "NUL"];
        private static readonly char[] DeviceDigits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '¹', '²', '³'];

        internal static readonly HashSet<string> ReservedNames =
            ReservedStems.Concat(DeviceDigits.SelectMany(d => new[] { "COM" + d, "LPT" + d }))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Codepoints 0-31, not char.IsControl: DEL and the C1 block are legal in a Windows filename.
        internal static bool IsIllegal(char c, bool windows) =>
            windows ? WindowsIllegal.Contains(c, StringComparison.Ordinal) || c <= '\u001F' : c is '/' or '\0';

        /// <summary>A name is reserved by its stem — everything before the first period.</summary>
        internal static bool IsReservedDeviceName(string segment) =>
            ReservedNames.Contains(segment.Split('.')[0]);
    }

    /// <summary>
    /// Which rule set a given flag combination lands on. The platform-dependent flag only relaxes anything
    /// off Windows, so on a Windows host both settings mean the same thing — which is exactly why the
    /// device-name and illegal-character invariants below are asserted against this and not unconditionally.
    /// </summary>
    private static bool WindowsRules(bool platformDependent) =>
        !platformDependent || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static FilePathCleaner Cleaner(bool remove, bool replace, bool platformDependent,
        Dictionary<string, string>? overrides = null) =>
        new(remove, replace, platformDependent, overrides ?? []);

    /// <summary>Cleans, or reports that the segment was rejected. The contract permits exactly these two outcomes.</summary>
    private static string? CleanOrReject(FilePathCleaner cleaner, string segment)
    {
        try
        {
            return cleaner.CleanPathSegment(segment);
        }
        catch (LuaRenamerException)
        {
            return null;
        }
    }

    private static void AssertClean(string result, bool windows)
    {
        result.Should().NotBeNullOrWhiteSpace();
        result.Where(c => Platform.IsIllegal(c, windows)).Should().BeEmpty();
        result.Should().NotStartWith(" ");
        result.Should().NotEndWith(" ");
        result.Should().NotEndWith(".");
        if (windows)
            Platform.IsReservedDeviceName(result).Should().BeFalse();
    }

    [Property]
    public void CleanedSegmentSatisfiesEveryRule(NonNull<string> segment, bool remove, bool replace, bool platformDependent)
    {
        if (CleanOrReject(Cleaner(remove, replace, platformDependent), segment.Get) is { } cleaned)
            AssertClean(cleaned, WindowsRules(platformDependent));
    }

    [Property]
    public void CleaningIsIdempotent(NonNull<string> segment, bool remove, bool replace, bool platformDependent)
    {
        FilePathCleaner cleaner = Cleaner(remove, replace, platformDependent);
        if (CleanOrReject(cleaner, segment.Get) is { } once)
            CleanOrReject(cleaner, once).Should().Be(once);
    }

    [Property]
    public void ReplacementThatIsItselfIllegalIsRejected(NonNull<string> segment, bool remove, bool replace)
    {
        // Windows rules are forced on so the check is meaningful regardless of host platform.
        FilePathCleaner cleaner = Cleaner(remove, replace, platformDependent: false,
            overrides: new Dictionary<string, string> { ["<"] = ":" });

        FluentActions.Invoking(() => cleaner.CleanPathSegment(segment.Get))
            .Should().Throw<LuaRenamerException>().WithMessage("Illegal path replacement character*");
    }

    [Theory]
    // The device-name rule is stem-based and case-insensitive, which produces asymmetries worth pinning:
    // a trailing period still names the device, but a longer stem does not.
    [InlineData("com1", true)]
    [InlineData("com1.test", true)]
    [InlineData("com\u00b9", true)]
    [InlineData("COM\u00b2", true)]
    [InlineData("COM\u00b3", true)]
    [InlineData("NUL", true)]
    [InlineData("COM1.", true)]
    [InlineData("CON1", false)]
    [InlineData("COM1test", false)]
    [InlineData("COM1test.test", false)]
    public void ReservedDeviceNames(string segment, bool rejected)
    {
        // platformDependentIllegalChars: false forces Windows rules, so this pins the same behavior on any host.
        var result = CleanOrReject(Cleaner(remove: false, replace: false, platformDependent: false), segment);
        if (rejected)
            result.Should().BeNull();
        else
            result.Should().Be(segment);
    }

    [Fact]
    public void EmbeddedNullIsReplaced()
    {
        // NUL is a control character, so it goes through the illegal-character path rather than being rejected.
        Cleaner(remove: false, replace: false, platformDependent: false)
            .CleanPathSegment("test\0test").Should().Be("test_test");
    }
}
