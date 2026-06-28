using System.ComponentModel;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

// Signature carrier for the shared getname resolver (see GetName). Anime/Episode/Tmdb all bind the
// same Lua closure, so one delegate describes them all; include_unofficial is honored by the closure
// regardless of receiver.
public delegate string? AnimeTitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang,
    [Description("Whether to include unofficial titles")] bool? include_unofficial);

public delegate string EpisodeNumbersDelegate(
    [Description("The amount of padding to use")] long pad);

public delegate void LogDelegate(
    [Description("The message to log")] string message);
