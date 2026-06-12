using System.ComponentModel;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

public delegate string? AnimeTitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang,
    [Description("Whether to include unofficial titles")] bool? include_unofficial);

public delegate string? TitleDelegate(
    [Description("Language to get the title in")] TitleLanguage lang);

public delegate string EpisodeNumbersDelegate(
    [Description("The amount of padding to use")] long pad);

public delegate void LogDelegate(
    [Description("The message to log")] string message);
