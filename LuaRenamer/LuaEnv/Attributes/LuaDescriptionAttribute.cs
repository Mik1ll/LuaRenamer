using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public class LuaDescriptionAttribute : Attribute
{
    public string Description { get; }

    public LuaDescriptionAttribute(string description) => Description = description;
}
