using System;
using System.Collections;
using System.Collections.Generic;
using NLua;

namespace LuaRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this Lua lua, LuaTable env, object obj, string name, Dictionary<object, LuaTable> cache = null)
        {
            cache ??= new Dictionary<object, LuaTable>();
            env[name] = AddHelper(lua, cache, obj);
        }

        // TODO: convert enum to a unique string
        private static object AddHelper(Lua lua, Dictionary<object, LuaTable> cache, object obj) => obj switch
        {
            IDictionary d => DictIteration(lua, cache, d),
            IList e => ListIteration(lua, cache, e),
            Enum e => Convert.ToInt64(e),
            _ => obj
        };

        private static LuaTable DictIteration(Lua lua, Dictionary<object, LuaTable> cache, IDictionary dict)
        {
            if (cache.TryGetValue(dict, out var luaTable))
                return luaTable;
            lua.NewTable("_");
            var tab = lua.GetTable("_");
            cache[dict] = tab;
            foreach (DictionaryEntry entry in dict)
            {
                tab[entry.Key is Enum e ? Convert.ToInt64(e) : entry.Key] = AddHelper(lua, cache, entry.Value);
            }
            return tab;
        }

        private static LuaTable ListIteration(Lua lua, Dictionary<object, LuaTable> cache, IEnumerable list)
        {
            if (cache.TryGetValue(list, out var luaTable))
                return luaTable;
            lua.NewTable("_");
            var tab = lua.GetTable("_");
            cache[list] = tab;
            var i = 1;
            foreach (var obj in list)
            {
                tab[i] = AddHelper(lua, cache, obj);
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
