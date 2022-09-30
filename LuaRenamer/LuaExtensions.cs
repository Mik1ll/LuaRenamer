using System;
using System.Collections;
using System.Collections.Generic;
using NLua;

namespace LuaRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this LuaContext lua, LuaTable env, object? obj, string name, Dictionary<object, LuaTable>? cache = null)
        {
            cache ??= new Dictionary<object, LuaTable>();
            env[name] = AddHelper(lua, cache, obj);
        }

        private static object? AddHelper(LuaContext lua, Dictionary<object, LuaTable> cache, object? obj) => obj switch
        {
            IDictionary d => DictIteration(lua, cache, d),
            IList e => ListIteration(lua, cache, e),
            _ => obj
        };

        private static LuaTable DictIteration(LuaContext lua, Dictionary<object, LuaTable> cache, IDictionary dict)
        {
            if (cache.TryGetValue(dict, out var luaTable))
                return luaTable;
            lua.NewTable("_");
            var tab = lua.GetTable("_");
            cache[dict] = tab;
            foreach (DictionaryEntry entry in dict)
            {
                tab[entry.Key] = AddHelper(lua, cache, entry.Value);
            }
            return tab;
        }

        private static LuaTable ListIteration(LuaContext lua, Dictionary<object, LuaTable> cache, IEnumerable list)
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

        public static Dictionary<string, object> ToTable(this DateTime dt)
        {
            return new Dictionary<string, object>
            {
                { LuaEnv.date.year, dt.Year },
                { LuaEnv.date.month, dt.Month },
                { LuaEnv.date.day, dt.Day },
                { LuaEnv.date.yday, dt.DayOfYear },
                { LuaEnv.date.wday, (long)dt.DayOfWeek + 1 },
                { LuaEnv.date.hour, dt.Hour },
                { LuaEnv.date.min, dt.Minute },
                { LuaEnv.date.sec, dt.Second },
                { LuaEnv.date.isdst, dt.IsDaylightSavingTime() }
            };
        }
    }
}
