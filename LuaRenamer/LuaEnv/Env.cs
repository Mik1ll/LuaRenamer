// ReSharper disable InconsistentNaming

namespace LuaRenamer.LuaEnv;

public class Env : Table
{
    public static readonly Env Inst = new();
    public string filename => Get();
    public string destination => Get();
    public string subfolder => Get();
    public string use_existing_anime_location => Get();
    public string replace_illegal_chars => Get();
    public string remove_illegal_chars => Get();
    public string skip_rename => Get();
    public string skip_move => Get();
    public File file => new() { Fn = Get() };
    public Anime anime => new() { Fn = Get() };
    public Array<Anime> animes => new() { Fn = Get() };
    public Episode episode => new() { Fn = Get() };
    public Array<Episode> episodes => new() { Fn = Get() };
    public Array<ImportFolder> importfolders => new() { Fn = Get() };
    public Group group => new() { Fn = Get() };
    public Array<Group> groups => new() { Fn = Get() };
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
