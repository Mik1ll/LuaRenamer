// ReSharper disable InconsistentNaming

using System;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv;

[LuaType(LuaTypeNames.Episode)]
public partial class EpisodeTable : LuaTableWriter
{
    internal EpisodeTable(LuaTable t) : base(t, _classidVal) { }

    [LuaField("Get the title in the specified language")]
    public required LuaFunctionRef<Func<TitleLanguage, string?>> getname { init => Set(value.Value); }

    [LuaField("Duration of the episode in seconds")]
    public required long duration { init => Set(value); }

    [LuaField("Episode number")]
    public required long number { init => Set(value); }

    [LuaField("Type of the episode")]
    public required EpisodeType type { init => Set(value.ToString()); }

    [LuaField("Air date of the episode")]
    public required LuaRef<DateTimeTable>? airdate { init => Set(value?.Table); }

    [LuaField("ID of the anime this episode belongs to")]
    public required long animeid { init => Set(value); }

    [LuaField("AniDB episode ID")]
    public required long id { init => Set(value); }

    [LuaField("All available titles for the episode")]
    public required LuaArray<LuaRef<TitleTable>> titles { init => Set(value.Table); }

    [LuaField("Episode number type prefix (e.g., '', 'C', 'S', 'T', 'P', 'O')")]
    public required string prefix { init => Set(value); }

    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";

    public static implicit operator LuaRef<EpisodeTable>(EpisodeTable t) => new(t._t);
}
