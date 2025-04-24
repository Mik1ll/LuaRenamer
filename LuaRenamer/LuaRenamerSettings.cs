using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;

namespace LuaRenamer;

public class LuaRenamerSettings
{
    [RenamerSetting(Type = RenamerSettingType.Code, Language = CodeLanguage.Lua)]
    public string Script { get; set; } = string.Empty;

    [RenamerSetting(Name = "Platform-Dependent Illegal Characters", Type = RenamerSettingType.Boolean,
        Description = "Check if you only want to replace/remove directory separators on linux")]
    public bool PlatformDependentIllegalCharacters { get; set; }
}
