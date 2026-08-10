using System;
using System.ComponentModel;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

// Every callable a user script can reach, in one place. A delegate type here is a *contract* — the
// signature the defs/names generators document — not an implementation. A contract is implemented either:
//
//   in C#   the model field is typed as the delegate, and a producer assigns a closure
//           (`[LuaField] public required LogDelegate log { get; init; }`)
//   in Lua  the model field is a LuaFunc<TContract> over a source chunk, and is static because a Lua
//           source is a constant (`[LuaField] public static LuaFunc<TitleDelegate> getname => ...`)
//
// ModelTranslator marshals a delegate as-is and compiles a LuaFunc; the generators read the contract off
// either through SchemaReflection.ContractOf.

public delegate string EpisodeNumbersDelegate(
    [Description("The amount of padding to use")] long pad);

public delegate void LogDelegate(
    [Description("The message to log")] string message);

// The two getname contracts. Anime exposes include_unofficial; Episode/Tmdb expose lang only, even though
// the shared source honors include_unofficial regardless — the narrower signature is the documented surface.
public delegate string? AnimeTitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang,
    [Description("Whether to include unofficial titles")] bool? include_unofficial);

public delegate string? TitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang);

/// <summary>Non-generic view for <see cref="ModelTranslator"/>, which compiles a callable without knowing its contract.</summary>
public interface ILuaFunc
{
    /// <summary>A chunk whose top-level <c>return function ... end</c> yields the callable.</summary>
    string Source { get; }
}

/// <summary>
/// A Lua implementation of <typeparamref name="TDelegate"/>. A pure description — just the source —
/// carrying no live Lua handle and no interpreter, so the model graph stays decoupled from any <c>Lua</c>
/// instance; <see cref="ModelTranslator"/> mints the <c>LuaFunction</c> on demand.
/// </summary>
/// <remarks>
/// Swapping an implementation is retyping the field, <c>LuaFunc&lt;TFoo&gt;</c> ⇄ <c>TFoo</c>, plus adding or
/// removing the producer assignment — the contract, the <c>[LuaField]</c> and the generated defs are untouched.
/// The one asymmetry: a <c>[LuaField(Method = true)]</c> callable is invoked <c>obj:fn()</c> with an implicit
/// <c>self</c> a CLR delegate cannot receive, so a method is effectively Lua-only; free functions
/// (<c>Method = false</c>, e.g. <see cref="EpisodeNumbersDelegate"/>) swap freely either way.
/// </remarks>
/// <typeparam name="TDelegate">Reflection-only: the contract the generators document, never invoked.</typeparam>
public sealed class LuaFunc<TDelegate>(string source) : ILuaFunc where TDelegate : Delegate
{
    public string Source { get; } = source;
}

/// <summary>The Lua-implemented callables the models expose, one instance per documented contract.</summary>
public static class LuaFunctions
{
    // Deliberately a plain literal, not an interpolated one: the enum members and model fields named below
    // are only part of what this depends on (lualinq's from/where/orderby/select are not checkable either
    // way), so nameof() bought a false sense of coverage. ModelTranslatorTests.GetName_Title_Priority_Policy
    // is what actually catches a rename.
    private const string GetNameSource =
        """
        ---@param self Anime|Episode
        ---@param lang Language
        ---@param include_unofficial? boolean
        ---@return string?
        return function (self, lang, include_unofficial)
            local title_priority = {
                Main = 0,
                Official = 1,
                None = 2,
                Synonym = include_unofficial and 3 or nil,
            }
            ---@type string?
            local name = from(self.titles):where(function(t1) ---@param t1 Title
                return t1.language == lang and title_priority[t1.type] ~= nil
            end):orderby(function(t2) ---@param t2 Title
                return title_priority[t2.type]
            end):select("name"):first()
            return name
        end
        """;

    /// <summary><c>anime:getname(lang, include_unofficial)</c>.</summary>
    public static readonly LuaFunc<AnimeTitleDelegate> AnimeGetName = new(GetNameSource);

    /// <summary><c>episode:getname(lang)</c> — same source as <see cref="AnimeGetName"/>, so the sandbox's
    /// source-keyed memo collapses both onto one compiled handle.</summary>
    public static readonly LuaFunc<TitleDelegate> GetName = new(GetNameSource);
}
