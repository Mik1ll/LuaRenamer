using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class LuaTypeAttribute : Attribute
{
    public string Type { get; }
    public bool Nillable { get; init; }
    public string DefaultValue { get; init; } = "nil";

    public LuaTypeAttribute(string type) => Type = type;
}
