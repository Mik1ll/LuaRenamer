using LuaRenamer.LuaEnv.BaseTypes;
using Shoko.Abstractions.Enums;

namespace LuaRenamer.LuaEnv;

public class EnumsTable : RootTable
{
    public static EnumTable<DropFolderType> ImportFolderType => new() { Fn = Get() };
    public static EnumTable<AnimeType> AnimeType => new() { Fn = Get() };
    public static EnumTable<EpisodeType> EpisodeType => new() { Fn = Get() };
    public static EnumTable<TitleType> TitleType => new() { Fn = Get() };
    public static EnumTable<TitleLanguage> Language => new() { Fn = Get() };
    public static EnumTable<RelationType> RelationType => new() { Fn = Get() };
}
