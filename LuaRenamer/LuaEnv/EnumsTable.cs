using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.LuaEnv;

public class EnumsTable : LuaRootTableWriter
{
    internal EnumsTable(LuaTable t,
        LuaEnumRef<DropFolderType> importFolderType,
        LuaEnumRef<AnimeType> animeType,
        LuaEnumRef<EpisodeType> episodeType,
        LuaEnumRef<TitleType> titleType,
        LuaEnumRef<TitleLanguage> language,
        LuaEnumRef<RelationType> relationType,
        LuaEnumRef<YearlySeason> seasonName) : base(t)
    {
        _t[nameof(ImportFolderType)] = importFolderType.Table;
        _t[nameof(AnimeType)] = animeType.Table;
        _t[nameof(EpisodeType)] = episodeType.Table;
        _t[nameof(TitleType)] = titleType.Table;
        _t[nameof(Language)] = language.Table;
        _t[nameof(RelationType)] = relationType.Table;
        _t[nameof(SeasonName)] = seasonName.Table;
    }

    public static EnumTable<DropFolderType> ImportFolderType => new() { Fn = Get() };
    public static EnumTable<AnimeType> AnimeType => new() { Fn = Get() };
    public static EnumTable<EpisodeType> EpisodeType => new() { Fn = Get() };
    public static EnumTable<TitleType> TitleType => new() { Fn = Get() };
    public static EnumTable<TitleLanguage> Language => new() { Fn = Get() };
    public static EnumTable<RelationType> RelationType => new() { Fn = Get() };
    public static EnumTable<YearlySeason> SeasonName => new() { Fn = Get() };
}
