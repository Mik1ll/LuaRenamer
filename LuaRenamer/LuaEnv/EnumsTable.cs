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
    public required LuaEnumRef<DropFolderType> importFolderType { init => Set(value.Table, "ImportFolderType"); }
    [LuaField]
    public required LuaEnumRef<AnimeType> animeType { init => Set(value.Table, "AnimeType"); }
    [LuaField]
    public required LuaEnumRef<EpisodeType> episodeType { init => Set(value.Table, "EpisodeType"); }
    [LuaField]
    public required LuaEnumRef<TitleType> titleType { init => Set(value.Table, "TitleType"); }
    [LuaField]
    public required LuaEnumRef<TitleLanguage> language { init => Set(value.Table, "Language"); }
    [LuaField]
    public required LuaEnumRef<RelationType> relationType { init => Set(value.Table, "RelationType"); }
    [LuaField]
    public required LuaEnumRef<YearlySeason> seasonName { init => Set(value.Table, "SeasonName"); }
}
