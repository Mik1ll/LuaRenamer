using System.Collections.Generic;
using NLua;

namespace ScriptRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this Lua lua, HashSet<string> envBuilder, object obj, string name)
        {
            envBuilder.Add($"{name} = {name},");
            lua[name] = obj;
        }

        public static LuaTable CreateEnv(this Lua lua, IEnumerable<string> envBuilder) =>
            (LuaTable)lua.DoString($"return {{{string.Join("", envBuilder)}}}")[0];
    }
}
