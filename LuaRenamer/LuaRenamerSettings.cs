using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;

namespace LuaRenamer;

public class LuaRenamerSettings
{
    [RenamerSetting(Type = RenamerSettingType.Code, Language = CodeLanguage.Lua)]
    public string Script { get; set; } = string.Empty;
}
