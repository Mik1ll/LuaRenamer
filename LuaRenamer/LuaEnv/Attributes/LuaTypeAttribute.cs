using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class LuaTypeAttribute : Attribute
{
    public string Type { get; }
    public string? Description { get; }
    public string DefaultValue { get; init; } = LuaTypeNames.nil;

    /// <summary>
    /// When true the field is written by the user script (an output) rather than bound by LuaContext.
    /// The builder source generator skips generating a setter for these fields.
    /// </summary>
    public bool Output { get; init; }

    public LuaTypeAttribute(string type, string? description = null) => (Type, Description) = (type, description);
}
