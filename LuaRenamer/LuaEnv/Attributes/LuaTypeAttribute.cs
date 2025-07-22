using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class)]
public class LuaTypeAttribute : Attribute
{
    public string Type { get; }
    public string? Description { get; }
    public string DefaultValue { get; init; } = LuaTypeNames.nil;

    public LuaTypeAttribute(string type, string? description = null) => (Type, Description) = (type, description);
}
