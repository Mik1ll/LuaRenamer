using AwesomeAssertions;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace LuaRenamer.Tests.Renaming;

/// <summary>What a relocation writes to the host's log — both what a script asks for directly and what the
/// shipped Lua library emits on its own.</summary>
public class LoggingTests
{
    private static FakeLogger<LuaRenamer> Run(string script)
    {
        var logger = new FakeLogger<LuaRenamer>();
        new LuaRenamer(logger).GetPath(RelocationGraph.Default().Context(script));
        return logger;
    }

    [Theory]
    [InlineData("logdebug", LogLevel.Debug)]
    [InlineData("log", LogLevel.Information)]
    [InlineData("logwarn", LogLevel.Warning)]
    [InlineData("logerror", LogLevel.Error)]
    public void EachScriptLogFunctionWritesAtItsOwnLevel(string function, LogLevel expected)
    {
        FakeLogger<LuaRenamer> logger = Run($"{function}('a message')");

        FakeLogRecord record = logger.Collector.GetSnapshot().Should()
            .ContainSingle(r => r.Message == "a message").Subject;
        record.Level.Should().Be(expected);
    }

    [Fact]
    public void LibraryEmittedDiagnosticsReachTheSameLog()
    {
        FakeLogger<LuaRenamer> logger = Run("linqSetLogLevel(3); from({'test1', 'test2'})");

        FakeLogRecord record = logger.Collector.GetSnapshot()
            .Should().ContainSingle(r => r.Message.StartsWith("LuaLinq: after fromArrayInstance", System.StringComparison.Ordinal)).Subject;
        record.Level.Should().Be(LogLevel.Debug);
        record.Message.Should().Contain("2 items");
    }

    [Fact]
    public void AFailedRelocationIsLoggedAsAWarning()
    {
        FakeLogger<LuaRenamer> logger = Run("error('boom')");

        logger.Collector.GetSnapshot().Should().Contain(r => r.Level == LogLevel.Warning);
    }
}
