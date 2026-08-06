using System.Text;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// How the <c>Language</c> enum is laid out in enums.lua. Every other enum is emitted as a flat list of its
/// values; <see cref="TitleLanguage"/> alone is split into folding regions — the handful AniDB actually uses
/// first, then the rest of AniDB's set alphabetically, then everything else, then the sentinels — because the
/// full list is long enough that script authors need it grouped.
/// </summary>
/// <remarks>
/// This is editorial policy about one specific Shoko enum, so it lives away from
/// <see cref="ModelDefsGenerator"/>, which knows nothing about any particular enum. The section order and
/// markers are decided here; how an individual <c>Name = "Name"</c> line reads stays with the caller.
/// </remarks>
internal static class TitleLanguageSections
{
    private const int Common = 0;
    private const int OtherAnidb = 1;
    private const int NonAnidb = 2;
    private const int Sentinel = 3;

    /// <summary>
    /// The body of the <c>Language</c> table — everything between its braces, with region markers and blank
    /// lines interleaved — or <c>null</c> for any other enum, which the caller then emits flat.
    /// <paramref name="renderMappings"/> turns a run of enum names into their Lua lines.
    /// </summary>
    /// <remarks>Takes the enum type so callers never have to name the one enum that is special.</remarks>
    internal static string? Render(Type enumType, Func<IEnumerable<string>, string> renderMappings)
    {
        if (enumType != typeof(TitleLanguage))
            return null;

        var lkup = Enum.GetValues<TitleLanguage>().ToLookup(t => t switch
        {
            TitleLanguage.Japanese or TitleLanguage.Romaji or TitleLanguage.English or TitleLanguage.Chinese or TitleLanguage.Pinyin
                or TitleLanguage.Korean or TitleLanguage.KoreanTranscription => Common,
            TitleLanguage.Unknown or TitleLanguage.Main or TitleLanguage.None => Sentinel,
            _ => AnidbLangs.Contains(t) ? OtherAnidb : NonAnidb,
        }, t => t.ToString());

        var sb = new StringBuilder();
        sb.Append("\n--#region AniDB Languages\n");
        sb.Append(renderMappings(lkup[Common]));
        sb.Append('\n');
        sb.Append(renderMappings(lkup[OtherAnidb].Order(StringComparer.Ordinal)));
        sb.Append("--#endregion\n");
        sb.Append("\n--#region Other Languages\n");
        sb.Append(renderMappings(lkup[NonAnidb].Order(StringComparer.Ordinal)));
        sb.Append("--#endregion\n\n");
        sb.Append(renderMappings(lkup[Sentinel]));
        return sb.ToString();
    }

    // The languages AniDB itself offers titles in. Anything outside this set lands in "Other Languages".
    private static readonly HashSet<TitleLanguage> AnidbLangs =
    [
        TitleLanguage.Japanese,
        TitleLanguage.Romaji,
        TitleLanguage.English,
        TitleLanguage.Chinese,
        TitleLanguage.ChineseSimplified,
        TitleLanguage.ChineseTraditional,
        TitleLanguage.Pinyin,
        TitleLanguage.Korean,
        TitleLanguage.KoreanTranscription,
        TitleLanguage.Afrikaans,
        TitleLanguage.Albanian,
        TitleLanguage.Arabic,
        TitleLanguage.Bengali,
        TitleLanguage.Bosnian,
        TitleLanguage.Bulgarian,
        TitleLanguage.MyanmarBurmese,
        TitleLanguage.Croatian,
        TitleLanguage.Czech,
        TitleLanguage.Danish,
        TitleLanguage.Dutch,
        TitleLanguage.Esperanto,
        TitleLanguage.Estonian,
        TitleLanguage.Filipino,
        TitleLanguage.Finnish,
        TitleLanguage.French,
        TitleLanguage.Georgian,
        TitleLanguage.German,
        TitleLanguage.Greek,
        TitleLanguage.HaitianCreole,
        TitleLanguage.Hebrew,
        TitleLanguage.Hindi,
        TitleLanguage.Hungarian,
        TitleLanguage.Icelandic,
        TitleLanguage.Indonesian,
        TitleLanguage.Italian,
        TitleLanguage.Javanese,
        TitleLanguage.Latin,
        TitleLanguage.Latvian,
        TitleLanguage.Lithuanian,
        TitleLanguage.Malaysian,
        TitleLanguage.Mongolian,
        TitleLanguage.Nepali,
        TitleLanguage.Norwegian,
        TitleLanguage.Persian,
        TitleLanguage.Polish,
        TitleLanguage.Portuguese,
        TitleLanguage.BrazilianPortuguese,
        TitleLanguage.Romanian,
        TitleLanguage.Russian,
        TitleLanguage.Serbian,
        TitleLanguage.Sinhala,
        TitleLanguage.Slovak,
        TitleLanguage.Slovenian,
        TitleLanguage.Spanish,
        TitleLanguage.Basque,
        TitleLanguage.Catalan,
        TitleLanguage.Galician,
        TitleLanguage.Swedish,
        TitleLanguage.Tamil,
        TitleLanguage.Tatar,
        TitleLanguage.Telugu,
        TitleLanguage.Thai,
        TitleLanguage.ThaiTranscription,
        TitleLanguage.Turkish,
        TitleLanguage.Ukrainian,
        TitleLanguage.Urdu,
        TitleLanguage.Vietnamese,
    ];
}
