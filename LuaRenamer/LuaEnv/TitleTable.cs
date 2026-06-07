// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Title)]
public partial class TitleTable : LuaTableWriter
{
    internal TitleTable(LuaTable t) : base(t) { }

    [LuaField("The title text")]
    public required string name { init => Set(value); }

    [LuaField("Language of the title")]
    public required TitleLanguage language { init => Set(value.ToString()); }

    [LuaField("ISO language code")]
    public required string languagecode { init => Set(value); }

    [LuaField("Type of the title")]
    public required TitleType type { init => Set(value.ToString()); }

    public static implicit operator LuaRef<TitleTable>(TitleTable t) => new(t._t);
}
