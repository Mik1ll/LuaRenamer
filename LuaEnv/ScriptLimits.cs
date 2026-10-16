namespace LuaRenamer.LuaEnv;

/// <summary>The resources one <see cref="LuaSandbox.Run"/> of a user script may use.</summary>
/// <param name="Instructions">
/// Lua VM instructions the script may execute, enforced to within 1,000. Time spent inside a single library
/// call (a pattern match, a sort without a comparator) executes no instructions and is not counted.
/// </param>
/// <param name="MemoryBytes">
/// The ceiling on the whole Lua state's allocated bytes while the script runs. It counts what translation and
/// the trusted chunks already hold, so it is an absolute size, not an allowance on top of them.
/// </param>
public sealed record ScriptLimits(long Instructions, long MemoryBytes)
{
    /// <summary>Set from the shipped default script's measured use against a fully populated environment, with a wide margin.</summary>
    public static ScriptLimits Default { get; } = new(10_000_000, 32 * 1024 * 1024);
}
