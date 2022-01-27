using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NLua;

namespace ScriptRenamer
{
    public static class LuaExtensions
    {
        public static void AddObject(this Lua lua, HashSet<string> envBuilder, object obj, string name)
        {
            envBuilder.Add($"{name} = {name},");
            switch (obj)
            {
                case IDictionary:
                    lua.NewTable(name);
                    lua[name] = DictIteration(lua, (Dictionary<string, object>)obj);
                    break;
                case IEnumerable<dynamic> e:
                    lua.NewTable(name);
                    lua[name] = ListIteration(lua, e);
                    break;
                default:
                    lua[name] = obj;
                    break;
            }
        }

        private static LuaTable DictIteration(Lua lua, Dictionary<string, object> dict)
        {
            lua.NewTable("newtab");
            var tab = lua.GetTable("newtab");
            foreach (var (key, obj) in dict)
            {
                tab[key] = obj switch
                {
                    IDictionary => DictIteration(lua, (Dictionary<string, object>)obj),
                    IEnumerable<dynamic> e => ListIteration(lua, e),
                    _ => obj
                };
            }
            return tab;
        }

        private static LuaTable ListIteration(Lua lua, IEnumerable<dynamic> list)
        {
            lua.NewTable("newtab");
            var tab = lua.GetTable("newtab");
            foreach (var (obj, i) in list.Select((o, i) => (o, i + 1)))
            {
                tab[i] = obj switch
                {
                    IDictionary => DictIteration(lua, (Dictionary<string, object>)obj),
                    IEnumerable<dynamic> e => ListIteration(lua, e),
                    _ => obj
                };
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
