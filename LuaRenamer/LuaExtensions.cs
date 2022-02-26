using System;
using System.Collections;
using System.Collections.Generic;
using NLua;

namespace LuaRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this Lua lua, LuaTable env, object obj, string name)
        {
            env[name] = AddHelper(lua, obj);
        }

        // TODO: convert enum to a unique string
        private static object AddHelper(Lua lua, object obj) => obj switch
        {
            IDictionary d => DictIteration(lua, d),
            IList e => ListIteration(lua, e),
            Enum e => Convert.ToInt64(e),
            _ => obj
        };

        private static LuaTable DictIteration(Lua lua, IDictionary dict)
        {
            lua.NewTable("_");
            var tab = lua.GetTable("_");
            foreach (DictionaryEntry entry in dict)
            {
                tab[entry.Key is Enum e ? Convert.ToInt64(e) : entry.Key] = AddHelper(lua, entry.Value);
            }
            return tab;
        }

        private static LuaTable ListIteration(Lua lua, IEnumerable list)
        {
            lua.NewTable("_");
            var tab = lua.GetTable("_");
            var i = 1;
            foreach (var obj in list)
            {
                tab[i] = AddHelper(lua, obj);
                i++;
            }
            return tab;
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
