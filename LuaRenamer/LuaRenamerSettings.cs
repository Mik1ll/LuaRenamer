using System.ComponentModel.DataAnnotations;
using LuaRenamer.LuaEnv;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace LuaRenamer;

public class LuaRenamerSettings : IRelocationProviderConfiguration
{
    [Display(Name = "Remove Illegal Characters",
        Description = "Check if you want to remove illegal characters from the filename entirely. " +
                      "If false, they will be replaced with underscores or replacement characters.")]
    public bool RemoveIllegalCharacters { get; set; }

    [Display(Name = "Replace Illegal Characters",
        Description = $"Check if you want to replace illegal characters with alternatives. " +
                      $"May be customized via `{nameof(EnvTable.illegal_chars_map)}`")]
    public bool ReplaceIllegalCharacters { get; set; }

    [Display(Name = "Use Existing Anime Location",
        Description = "Check if you want to reuse the existing location of files from the same series. " +
                      "Takes precedence over `destination` and `subfolder`. ")]
    public bool UseExistingAnimeLocation { get; set; }

    [Display(Name = "Platform-Dependent Illegal Characters",
        Description = "Check if you only want to replace/remove directory separators on linux.")]
    public bool PlatformDependentIllegalCharacters { get; set; }

    [CodeEditor(CodeEditorLanguage.Lua)]
    public string Script { get; set; } = string.Empty;
}
