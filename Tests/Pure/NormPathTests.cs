using System.IO;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace LuaRenamer.Tests.Pure;

public class NormPathTests
{
    [Property]
    public void IsIdempotent(NonNull<string> path)
    {
        var once = path.Get.NormPath();
        once.NormPath().Should().Be(once);
    }

    [Property]
    public void LeavesNoTrailingSeparator(NonNull<string> path)
    {
        var normalized = path.Get.NormPath();
        // A root ("C:\", "/", "\\?\") is all separator, so there is nothing to trim beyond it — treating that
        // as a violation would assert against the platform rather than against this project.
        if (normalized == Path.GetPathRoot(normalized)) return;
        Path.EndsInDirectorySeparator(normalized).Should().BeFalse();
    }

    [Property]
    public void UsesOnlyThePlatformSeparator(NonNull<string> path)
    {
        // Not NotContain(Alt): on Unix both separators are '/', which would demand a path with no separator at
        // all. A prefix rather than equality because the trailing trim is LeavesNoTrailingSeparator's to pin.
        var rewritten = path.Get.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        rewritten.Should().StartWith(path.Get.NormPath());
    }

    [Fact]
    public void KnownNormalizations()
    {
        @"a/b/c".NormPath().Should().Be(Path.Combine("a", "b", "c"));
        (Path.Combine("a", "b") + Path.DirectorySeparatorChar).NormPath().Should().Be(Path.Combine("a", "b"));
        "".NormPath().Should().BeEmpty();
        // Path.TrimEndingDirectorySeparator trims one separator per call (Mik1ll/LuaRenamer#170).
        "a/b///".NormPath().Should().Be(Path.Combine("a", "b"));
        "/".NormPath().Should().Be(Path.DirectorySeparatorChar.ToString());
    }
}
