// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class EnvTable : Table
{
    public static readonly EnvTable Inst = new();
    public string filename => Get();
    public string destination => Get();
    public string subfolder => Get();
    public string use_existing_anime_location => Get();
    public string replace_illegal_chars => Get();
    public string remove_illegal_chars => Get();
    public string skip_rename => Get();
    public string skip_move => Get();
    public string illegal_chars_map => Get();
    public FileTable file => new() { Fn = Get() };
    public AnimeTable anime => new() { Fn = Get() };
    public ArrayTable<AnimeTable> animes => new() { Fn = Get() };
    public EpisodeTable episode => new() { Fn = Get() };
    public ArrayTable<EpisodeTable> episodes => new() { Fn = Get() };
    public ArrayTable<ImportFolderTable> importfolders => new() { Fn = Get() };
    public GroupTable group => new() { Fn = Get() };
    public ArrayTable<GroupTable> groups => new() { Fn = Get() };
    public TmdbTable tmdb => new() { Fn = Get() };
    public string AnimeType => Get();
    public string TitleType => Get();
    public string Language => Get();
    public string EpisodeType => Get();
    public string ImportFolderType => Get();
    public string RelationType => Get();
    public string episode_numbers => Get();
    public string logdebug => Get();
    public string log => Get();
    public string logwarn => Get();
    public string logerror => Get();
}
