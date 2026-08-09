using System;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// Marks a bound data property on a schema table class. The Lua type is inferred from the C# property type.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LuaFieldAttribute : Attribute
{
    public string? Description { get; }

    /// <summary>The value the field is initialized to in the generated <c>env.lua</c>.</summary>
    public string DefaultValue { get; init; } = "nil";

    /// <summary>
    /// When true a callable field uses Lua method-call syntax (<c>obj:fn()</c>, implicit self) rather
    /// than plain function syntax (<c>obj.fn()</c>).
    /// </summary>
    public bool Method { get; init; }

    public LuaFieldAttribute(string? description = null) => Description = description;
}
