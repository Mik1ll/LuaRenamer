using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLua;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    public class NLuaSingleton
    {
        public Lua Inst { get; } = new();
        private readonly LuaFunction _runSandboxed;
        public LuaTable Env { get; private set; }

        #region Sandbox

        const string BaseEnv = @"
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

        const string SandboxFunction = @"
function runsandboxed(untrusted_code, env)
  local untrusted_function, message = load(untrusted_code, nil, 't', env)
  if not untrusted_function then return nil, message end
  return untrusted_function()
end
return runsandboxed
";

        #endregion


        public NLuaSingleton()
        {
            Inst.State.Encoding = Encoding.UTF8;
            _runSandboxed = (LuaFunction)Inst.DoString(SandboxFunction)[0];
        }

        public object[] RunSandboxed(string code, Dictionary<string, object> env)
        {
            var envBuilder = new HashSet<string> { BaseEnv };
            foreach (var (k, v) in env)
                Inst.AddObject(envBuilder, v, k);

            Inst.AddObject(envBuilder, Enum.GetValues(typeof(AnimeType)).Cast<AnimeType>().ToDictionary(a => a.ToString(), a => Convert.ChangeType(a, TypeCode.Int32)), "AnimeType");
            Inst.AddObject(envBuilder, Enum.GetValues(typeof(TitleType)).Cast<TitleType>().ToDictionary(a => a.ToString(), a => Convert.ChangeType(a, TypeCode.Int32)), "TitleType");
            Inst.AddObject(envBuilder, Enum.GetValues(typeof(TitleLanguage)).Cast<TitleLanguage>().ToDictionary(a => a.ToString(), a => Convert.ChangeType(a, TypeCode.Int32)), "TitleLanguage");
            Inst.AddObject(envBuilder, Enum.GetValues(typeof(EpisodeType)).Cast<EpisodeType>().ToDictionary(a => a.ToString(), a => Convert.ChangeType(a, TypeCode.Int32)), "EpisodeType");
            Env = Inst.CreateEnv(envBuilder);
            return _runSandboxed.Call(code, Env);
        }

        ~NLuaSingleton()
        {
            Inst.Dispose();
        }
    }
}
