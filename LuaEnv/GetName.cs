using System;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// The shared title resolver bound into every Anime/Episode/Tmdb table as the <c>getname</c> method
/// (<c>self:getname(lang, include_unofficial)</c>). Its body is written in Lua (<see cref="Definition"/>),
/// so the runtime value is a real <see cref="LuaFunction"/> handle; <see cref="AnimeTitleDelegate"/> supplies
/// the signature the defs/names generators document. One closure is shared across all tables, hence the
/// <c>self</c> parameter and ':' method-call syntax.
/// </summary>
public sealed class GetName : LuaFunctionDef<AnimeTitleDelegate>
{
    private GetName(int reference, Lua interpreter) : base(reference, interpreter) { }

    /// <summary>The Lua source, run once per env build (against the sandbox env) to produce the closure.</summary>
    public static readonly string Definition =
        $$"""
          ---@param self Anime|Episode
          ---@param lang Language
          ---@param include_unofficial? boolean
          ---@return string?
          return function (self, lang, include_unofficial)
              local title_priority = {
                  {{nameof(TitleType.Main)}} = 0,
                  {{nameof(TitleType.Official)}} = 1,
                  {{nameof(TitleType.None)}} = 2,
                  {{nameof(TitleType.Synonym)}} = include_unofficial and 3 or nil,
              }
              ---@type string?
              local name = from(self.{{nameof(AnimeModel.titles)}}):where(function(t1) ---@param t1 Title
                  return t1.{{nameof(TitleModel.language)}} == lang and title_priority[t1.{{nameof(TitleModel.type)}}] ~= nil
              end):orderby(function(t2) ---@param t2 Title
                  return title_priority[t2.{{nameof(TitleModel.type)}}]
              end):select("{{nameof(TitleModel.name)}}"):first()
              return name
          end
          """;

    /// <summary>
    /// Runs <see cref="Definition"/> through <paramref name="runSandboxed"/> (bound to the sandbox
    /// <paramref name="env"/> so lualinq's <c>from</c> resolves) and re-homes the resulting handle as a
    /// <see cref="GetName"/>.
    /// </summary>
    public static GetName Create(LuaFunction runSandboxed, LuaTable env, Lua interpreter)
    {
        var fn = (LuaFunction)runSandboxed.Call(Definition, env)[1];
        return Wrap(fn, interpreter);
    }

    /// <summary>
    /// Re-homes an existing Lua function handle as a <see cref="GetName"/> (used by <see cref="Create"/>,
    /// and by tests that supply their own stand-in closure). The new instance and <paramref name="fn"/>
    /// share one registry reference (<see cref="object.GetHashCode"/> returns the lua ref), so ownership
    /// transfers here: suppress <paramref name="fn"/>'s finalizer to avoid unref-ing the handle out from
    /// under the returned <see cref="GetName"/>.
    /// </summary>
    internal static GetName Wrap(LuaFunction fn, Lua interpreter)
    {
        GC.SuppressFinalize(fn);
        return new GetName(fn.GetHashCode(), interpreter);
    }
}
