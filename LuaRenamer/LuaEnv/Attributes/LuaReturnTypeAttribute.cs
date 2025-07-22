using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class LuaReturnTypeAttribute : Attribute
{
    public string Type { get; }

    public LuaReturnTypeAttribute(string type) => Type = type;
}
