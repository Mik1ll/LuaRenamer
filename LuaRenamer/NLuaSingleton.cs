using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NLua;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer
{
    public class NLuaSingleton
    {
        public Lua Inst { get; } = new();
        public readonly LuaFunction LuaRunSandboxed;
        private readonly LuaFunction _readonly;
        public readonly HashSet<string> BaseEnvStrings = new() { BaseEnv, LuaLinqEnv };
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
            LuaRunSandboxed = (LuaFunction)Inst.DoString(SandboxFunction)[0];
            _readonly = (LuaFunction)Inst.DoString(ReadOnlyFunction)[0];
            _globalEnv = Inst.GetTable("_G");
            Inst.DoFile(_luaLinqLocation);
            AddGlobalReadOnlyTable(ConvertEnum<AnimeType>(), LuaEnv.AnimeType);
            AddGlobalReadOnlyTable(ConvertEnum<TitleType>(), LuaEnv.TitleType);
            AddGlobalReadOnlyTable(ConvertEnum<TitleLanguage>(), LuaEnv.Language);
            AddGlobalReadOnlyTable(ConvertEnum<EpisodeType>(), LuaEnv.EpisodeType);
            AddGlobalReadOnlyTable(ConvertEnum<DropFolderType>(), LuaEnv.DropFolderType);
        }

        private void AddGlobalReadOnlyTable(object obj, string name)
        {
            Inst.AddObject(_globalEnv, obj, name);
            _globalEnv[name] = _readonly.Call(_globalEnv[name])[0];
            BaseEnvStrings.Add($"{name} = {name},");
        }


        private static Dictionary<string, T> ConvertEnum<T>() =>
            Enum.GetValues(typeof(T)).Cast<T>().ToDictionary(a => a.ToString(), a => a);

        ~NLuaSingleton()
        {
            Inst.Dispose();
        }
    }
}
