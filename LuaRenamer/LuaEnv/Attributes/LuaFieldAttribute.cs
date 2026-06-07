using System;

namespace LuaRenamer.LuaEnv.Attributes;

/// <summary>
/// Marks a bound data property on a schema table class. The Lua type is inferred from the C# property
/// type; no type string is needed. Use <see cref="LuaTypeAttribute"/> for function members, complex
/// output union types, and class-level Lua name annotations.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LuaFieldAttribute : Attribute
{
    public string? Description { get; }
    public string DefaultValue { get; init; } = LuaTypeNames.nil;

    /// <summary>
    /// When true the field is written by the user script (an output) rather than bound by LuaContext.
    /// The builder source generator skips generating a setter for these fields.
    /// </summary>
    public bool Output { get; init; }

    public LuaFieldAttribute(string? description = null) => Description = description;
}
