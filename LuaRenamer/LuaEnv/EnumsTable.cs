using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace LuaRenamer.LuaEnv;

public class EnumsTable : LuaRootTableWriter
{
    internal EnumsTable(LuaTable t) : base(t) { }

    [LuaField]
    public required LuaEnumRef<DropFolderType> importFolderType { init => Set(value.Table, nameof(ImportFolderType)); }
    [LuaField]
    public required LuaEnumRef<AnimeType> animeType { init => Set(value.Table, nameof(AnimeType)); }
    [LuaField]
    public required LuaEnumRef<EpisodeType> episodeType { init => Set(value.Table, nameof(EpisodeType)); }
    [LuaField]
    public required LuaEnumRef<TitleType> titleType { init => Set(value.Table, nameof(TitleType)); }
    [LuaField]
    public required LuaEnumRef<TitleLanguage> language { init => Set(value.Table, nameof(Language)); }
    [LuaField]
    public required LuaEnumRef<RelationType> relationType { init => Set(value.Table, nameof(RelationType)); }
    [LuaField]
    public required LuaEnumRef<YearlySeason> seasonName { init => Set(value.Table, nameof(SeasonName)); }

    public static EnumTable<DropFolderType> ImportFolderType => new() { Fn = Get() };
    public static EnumTable<AnimeType> AnimeType => new() { Fn = Get() };
    public static EnumTable<EpisodeType> EpisodeType => new() { Fn = Get() };
    public static EnumTable<TitleType> TitleType => new() { Fn = Get() };
    public static EnumTable<TitleLanguage> Language => new() { Fn = Get() };
    public static EnumTable<RelationType> RelationType => new() { Fn = Get() };
    public static EnumTable<YearlySeason> SeasonName => new() { Fn = Get() };
}
