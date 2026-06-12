// ReSharper disable InconsistentNaming

using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

public partial class AniDbMediaTable : LuaTableWriter
{
    internal AniDbMediaTable(LuaTable t) : base(t) { }

    [LuaField("List of subtitle languages available in the release")]
    public required LuaArray<TitleLanguage> sublanguages { init => Set(value.Table); }

    [LuaField("List of audio languages available in the release")]
    public required LuaArray<TitleLanguage> dublanguages { init => Set(value.Table); }

    public static implicit operator LuaRef<AniDbMediaTable>(AniDbMediaTable t) => new(t._t);
}
