using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NLua;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    public class NLuaSingleton
    {
        public Lua Inst { get; } = new();
        private readonly LuaFunction _runSandboxed;
        private readonly LuaFunction _readonly;
        private readonly HashSet<string> _envBuilder = new() { BaseEnv, LuaLinqEnv };
        private readonly LuaTable _globalEnv;
        private readonly string _luaLinqLocation =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "lualinq.lua";

        #region Sandbox

        private const string BaseEnv = @"
ipairs = ipairs,
next = next,
pairs = pairs,
pcall = pcall,
tonumber = tonumber,
tostring = tostring,
type = type,
select = select,
string = { byte = string.byte, char = string.char, find = string.find, 
  format = string.format, gmatch = string.gmatch, gsub = string.gsub, 
  len = string.len, lower = string.lower, match = string.match, 
  rep = string.rep, reverse = string.reverse, sub = string.sub, 
  upper = string.upper },
table = { concat = table.concat, insert = table.insert, move = table.move, pack = table.pack, remove = table.remove, 
  sort = table.sort, unpack = table.unpack },
math = { abs = math.abs, acos = math.acos, asin = math.asin, 
  atan = math.atan, ceil = math.ceil, cos = math.cos, 
  deg = math.deg, exp = math.exp, floor = math.floor, 
  fmod = math.fmod, huge = math.huge, 
  log = math.log, max = math.max, maxinteger = math.maxinteger,
  min = math.min, mininteger = math.mininteger, modf = math.modf, pi = math.pi,
  rad = math.rad, random = math.random, randomseed = math.randomseed, sin = math.sin,
  sqrt = math.sqrt, tan = math.tan, tointeger = math.tointeger, type = math.type, ult = math.ult },
os = { clock = os.clock, difftime = os.difftime, time = os.time, date = os.date },
";

        private const string LuaLinqEnv = @"
from = from,
fromArray = fromArray,
fromArrayInstance = fromArrayInstance,
fromDictionary = fromDictionary,
fromIterator = fromIterator,
fromIteratorsArray = fromIteratorsArray,
fromSet = fromSet,
fromNothing = fromNothing,
";

        private const string SandboxFunction = @"
return function (untrusted_code, env)
  local untrusted_function, message = load(untrusted_code, nil, 't', env)
  if not untrusted_function then return nil, message end
  return untrusted_function()
end
";
        private const string ReadOnlyFunction = @"
return function (t)
  local proxy = {}
  local mt = {
    __index = t,
    __newindex = function (t,k,v)
      error(""attempt to update a read-only table"", 2)
    end
  }
  setmetatable(proxy, mt)
  return proxy
end
";

        #endregion


        public NLuaSingleton()
        {
            Inst.State.Encoding = Encoding.UTF8;
            _runSandboxed = (LuaFunction)Inst.DoString(SandboxFunction)[0];
            _readonly = (LuaFunction)Inst.DoString(ReadOnlyFunction)[0];
            _globalEnv = Inst.GetTable("_G");
            Inst.DoFile(_luaLinqLocation);
            AddGlobalReadOnlyTable(ConvertEnum<AnimeType>(), LuaEnv.AnimeType);
            AddGlobalReadOnlyTable(ConvertEnum<TitleType>(), LuaEnv.TitleType);
            AddGlobalReadOnlyTable(ConvertEnum<TitleLanguage>(), LuaEnv.Language);
            AddGlobalReadOnlyTable(ConvertEnum<EpisodeType>(), LuaEnv.EpisodeType);
            AddGlobalReadOnlyTable(ConvertEnum<DropFolderType>(), LuaEnv.DropFolderType);
            AddGlobalReadOnlyTable(new Dictionary<int, string>
            {
                { Convert.ToInt32(EpisodeType.Episode), "" },
                { Convert.ToInt32(EpisodeType.Special), "S" },
                { Convert.ToInt32(EpisodeType.Credits), "C" },
                { Convert.ToInt32(EpisodeType.Other), "O" },
                { Convert.ToInt32(EpisodeType.Parody), "P" },
                { Convert.ToInt32(EpisodeType.Trailer), "T" }
            }, LuaEnv.EpNumPrefix);
        }

        private void AddGlobalReadOnlyTable(object obj, string name)
        {
            Inst.AddObject(_globalEnv, obj, name);
            _globalEnv[name] = _readonly.Call(_globalEnv[name])[0];
            _envBuilder.Add($"{name} = {name},");
        }

        public (object[] retVal, LuaTable env) RunSandboxed(string code, Dictionary<string, object> env)
        {
            var luaEnv = Inst.CreateEnv(_envBuilder);
            Inst["env"] = luaEnv;
            foreach (var (k, v) in env)
                Inst.AddObject(luaEnv, v, k);
            var luaAnime = (LuaTable)((LuaTable)luaEnv[LuaEnv.Animes])[1];
            luaEnv[LuaEnv.Anime] = luaAnime;
            var luaEpisode = Inst.GetTableDict((LuaTable)luaEnv[LuaEnv.Episodes]).Where(e => (long)((LuaTable)e.Value)["animeid"] == (long)luaAnime["animeid"])
                .OrderBy(e => (long)((LuaTable)e.Value)["type"] == (int)EpisodeType.Other ? int.MinValue : (long)((LuaTable)e.Value)["type"])
                .ThenBy(e => (long)((LuaTable)e.Value)["number"])
                .First().Value;
            luaEnv[LuaEnv.Episode] = luaEpisode;
            return (_runSandboxed.Call(code, luaEnv), luaEnv);
        }

        private static Dictionary<string, int> ConvertEnum<T>() =>
            Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a.ToString(), a => Convert.ToInt32(a));

        ~NLuaSingleton()
        {
            Inst.Dispose();
        }
    }
}
