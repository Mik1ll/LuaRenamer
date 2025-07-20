using System;

namespace LuaRenamer.LuaEnv.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class LuaParameterAttribute : Attribute
{
    public string Name { get; }
    public string Type { get; }
    public string Description { get; }

    public LuaParameterAttribute(string name, string type, string description) => (Name, Type, Description) = (name, type, description);
}
