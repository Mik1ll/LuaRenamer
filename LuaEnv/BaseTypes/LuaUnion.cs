namespace LuaRenamer.LuaEnv.BaseTypes;

/// Compile-time-only marker for a Lua union type (T1|T2) on output properties.
/// Never instantiated at runtime; exists so generators can infer the Lua type from the C# property type.
public readonly struct LuaUnion<T1, T2>;
