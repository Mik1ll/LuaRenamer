using System;
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
    
        public static Dictionary<string, object> ToTable(this DateTime dt)
        {
            return new Dictionary<string, object>
            {
                { "year", dt.Year },
                { "month", dt.Month },
                { "day", dt.Day },
                { "yday", dt.DayOfYear },
                { "wday", (long)dt.DayOfWeek + 1 },
                { "hour", dt.Hour },
                { "min", dt.Minute },
                { "sec", dt.Second },
                { "isdst", dt.IsDaylightSavingTime() }
            };
        }
    }
}
