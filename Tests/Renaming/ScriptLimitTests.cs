using System.Threading.Tasks;
using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using Shoko.Abstractions.Video.Relocation;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>
/// A script over a limit, through the whole relocation under the limits it ships with. Runs on its own task
/// with a timeout for the same reason as the sandbox's resource-limit tests: a regression never returns.
/// </summary>
public class ScriptLimitTests
{
    [Theory(Timeout = 30_000)]
    [InlineData("while true do end", "instruction budget")]
    [InlineData("local t = {} while true do t[#t + 1] = {} end", "memory bound")]
    public async Task AScriptOverALimitFailsItsRelocationAndTheNextRunsNormally(string script, string limit)
    {
        (RelocationResult exceeded, RelocationResult next) = await Task.Run(() =>
        {
            var renamer = new LuaRenamer(new FakeLogger<LuaRenamer>());
            return (renamer.GetPath(RelocationGraph.Default().Context(script)),
                renamer.GetPath(RelocationGraph.Default().Context("filename = 'renamed'")));
        }, TestContext.Current.CancellationToken);

        exceeded.Error.Should().NotBeNull();
        exceeded.Error!.Message.Should().Contain(limit);
        next.Error.Should().BeNull();
        next.FileName.Should().Be("renamed.mp4");
    }
}
