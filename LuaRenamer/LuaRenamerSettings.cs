using LuaRenamer.LuaEnv;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace LuaRenamer;

public class LuaRenamerSettings
{
    [RenamerSetting(Type = RenamerSettingType.Code, Language = CodeLanguage.Lua)]
    public string Script { get; set; } = string.Empty;

    [RenamerSetting(Name = "Remove Illegal Characters", Type = RenamerSettingType.Boolean,
        Description = "Check if you want to remove illegal characters from the filename entirely. " +
                      "If false, they will be replaced with underscores or replacement characters.")]
    public bool RemoveIllegalCharacters { get; set; }

    [RenamerSetting(Name = "Replace Illegal Characters", Type = RenamerSettingType.Boolean,
        Description = $"Check if you want to replace illegal characters with alternatives. " +
                      $"May be customized via `{nameof(EnvTable.illegal_chars_map)}`")]
    public bool ReplaceIllegalCharacters { get; set; }

    [RenamerSetting(Name = "Use Existing Anime Location", Type = RenamerSettingType.Boolean,
        Description = "Check if you want to reuse the existing location of files from the same series. " +
                      "Takes precedence over `destination` and `subfolder`.")]
    public bool UseExistingAnimeLocation { get; set; }

    [RenamerSetting(Name = "Platform-Dependent Illegal Characters", Type = RenamerSettingType.Boolean,
        Description = "Check if you only want to replace/remove directory separators on linux.")]
    public bool PlatformDependentIllegalCharacters { get; set; }
}
