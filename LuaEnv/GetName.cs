using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

/// <summary>
/// The shared title-resolver Lua source, bound into every Anime/Episode/Tmdb table as the <c>getname</c>
/// method (<c>self:getname(lang, include_unofficial)</c>). One closure body, two typed wrappers
/// (<see cref="AnimeGetName"/>, <see cref="TitleGetName"/>), so the generators can document each table's
/// signature without duplicating the Lua. The closure is shared per receiver kind, hence the <c>self</c>
/// parameter and ':' method-call syntax.
/// </summary>
internal static class GetNameSource
{
    public static readonly string Lua =
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
}

/// <summary>
/// <c>getname</c> for Anime tables — <c>self:getname(lang, include_unofficial)</c>. A pure descriptor over
/// the shared <see cref="GetNameSource"/> closure; <see cref="AnimeTitleDelegate"/> supplies the documented
/// signature. <see cref="LuaSerializer"/> mints the live Lua handle from <see cref="Source"/>.
/// </summary>
public sealed class AnimeGetName : LuaFunctionDef<AnimeTitleDelegate>
{
    public override string Source => GetNameSource.Lua;
}

/// <summary>
/// <c>getname</c> for Episode/Tmdb tables — <c>self:getname(lang)</c>. Same shared closure as
/// <see cref="AnimeGetName"/>, but typed with <see cref="TitleDelegate"/> so the docs omit include_unofficial.
/// </summary>
public sealed class TitleGetName : LuaFunctionDef<TitleDelegate>
{
    public override string Source => GetNameSource.Lua;
}
