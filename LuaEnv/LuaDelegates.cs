using System.ComponentModel;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

// Signature carriers for the shared getname resolver (one Lua closure, see GetNameSource). Anime exposes
// include_unofficial (AnimeGetName); Episode/Tmdb expose lang only (TitleGetName) even though the closure
// honors include_unofficial regardless — the narrower signature is the documented surface for those tables.
public delegate string? AnimeTitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang,
    [Description("Whether to include unofficial titles")] bool? include_unofficial);

public delegate string? TitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang);

public delegate string EpisodeNumbersDelegate(
    [Description("The amount of padding to use")] long pad);

public delegate void LogDelegate(
    [Description("The message to log")] string message);
