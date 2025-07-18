using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaRenamer.LuaEnv;

public class EnumsTable : RootTable
{
    [LuaType(nameof(AnimeType))]
    [LuaDescription("Enum: Types of anime (TV, Movie, etc.)")]
    public static EnumTable<AnimeType> AnimeType => new() { Fn = Get() };

    [LuaType(nameof(TitleType))]
    [LuaDescription("Enum: Types of titles (Main, Official, etc.)")]
    public static EnumTable<TitleType> TitleType => new() { Fn = Get() };

    [LuaType(nameof(Language))]
    [LuaDescription("Enum: Available languages for titles")]
    public static EnumTable<TitleLanguage> Language => new() { Fn = Get() };

    [LuaType(nameof(EpisodeType))]
    [LuaDescription("Enum: Types of episodes (Normal, Special, etc.)")]
    public static EnumTable<EpisodeType> EpisodeType => new() { Fn = Get() };

    [LuaType(nameof(ImportFolderType))]
    [LuaDescription("Enum: Types of import folders")]
    public static EnumTable<DropFolderType> ImportFolderType => new() { Fn = Get() };

    [LuaType(nameof(RelationType))]
    [LuaDescription("Enum: Types of relations between anime")]
    public static EnumTable<RelationType> RelationType => new() { Fn = Get() };
}
