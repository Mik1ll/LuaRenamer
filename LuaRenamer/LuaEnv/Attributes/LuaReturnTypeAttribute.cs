using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class LuaReturnTypeAttribute : Attribute
{
    public string Type { get; }
    public bool Nillable { get; init; }

    public LuaReturnTypeAttribute(string type) => Type = type;
}
