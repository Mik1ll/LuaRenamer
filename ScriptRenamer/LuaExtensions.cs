using System;
using System.Collections;
using System.Collections.Generic;
using NLua;

namespace ScriptRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this Lua lua, LuaTable env, object obj, string name)
        {
            env[name] = obj switch
            {
                IDictionary d => DictIteration(lua, d),
                IList e => ListIteration(lua, e),
                _ => obj
            };
        }

        private static LuaTable DictIteration(Lua lua, IDictionary dict)
        {
            lua.NewTable("newTable");
            var tab = lua.GetTable("newTable");
            foreach (DictionaryEntry entry in dict)
            {
                tab[entry.Key] = entry.Value switch
                {
                    IDictionary d => DictIteration(lua, d),
                    IList e => ListIteration(lua, e),
                    _ => entry.Value
                };
            }
            return tab;
        }

        private static LuaTable ListIteration(Lua lua, IEnumerable list)
        {
            lua.NewTable("newTable");
            var tab = lua.GetTable("newTable");
            var i = 1;
            foreach (var obj in list)
            {
                tab[i] = obj switch
                {
                    IDictionary d => DictIteration(lua, d),
                    IList e => ListIteration(lua, e),
                    _ => obj
                };
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
