// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberHidesStaticFromOuterClass

#pragma warning disable CS8981
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
    public Anime anime => new() { Fn = Get() };
    public Array<Anime> animes => new() { Fn = Get() };
    public Episode episode => new() { Fn = Get() };
    public Array<Episode> episodes => new() { Fn = Get() };
    public Array<Importfolder> importfolders => new() { Fn = Get() };
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


    public static class date
    {
        public const string year = nameof(year);
        public const string month = nameof(month);
        public const string day = nameof(day);
        public const string yday = nameof(yday);
        public const string wday = nameof(wday);
        public const string hour = nameof(hour);
        public const string min = nameof(min);
        public const string sec = nameof(sec);
        public const string isdst = nameof(isdst);
    }

    public class @file : Table
    {
        public const string N = nameof(file);
        public const string Fn = N;

        public const string name = nameof(name);
        public const string nameFn = Fn + "." + name;
        public const string extension = nameof(extension);
        public const string extensionFn = Fn + "." + extension;
        public const string path = nameof(path);
        public const string pathFn = Fn + "." + path;
        public const string size = nameof(size);
        public const string sizeFn = Fn + "." + size;
        public Importfolder importfolder => new() { Fn = Get() };
        public const string earliestname = nameof(earliestname);
        public const string earliestnameFn = Fn + "." + earliestname;
        public Media media => new() { Fn = Get() };

        public static class hashes
        {
            public const string N = nameof(hashes);
            public const string Fn = file.Fn + "." + N;

            public const string crc = nameof(crc);
            public const string crcFn = Fn + "." + crc;
            public const string md5 = nameof(md5);
            public const string md5Fn = Fn + "." + md5;
            public const string ed2k = nameof(ed2k);
            public const string ed2kFn = Fn + "." + ed2k;
            public const string sha1 = nameof(sha1);
            public const string sha1Fn = Fn + "." + sha1;
        }

        public static class anidb
        {
            public const string N = nameof(anidb);
            public const string Fn = file.Fn + "." + N;

            public const string id = nameof(id);
            public const string idFn = Fn + "." + id;
            public const string censored = nameof(censored);
            public const string censoredFn = Fn + "." + censored;
            public const string source = nameof(source);
            public const string sourceFn = Fn + "." + source;
            public const string version = nameof(version);
            public const string versionFn = Fn + "." + version;
            public const string releasedate = nameof(releasedate);
            public const string releasedateFn = Fn + "." + releasedate;
            public const string description = nameof(description);
            public const string descriptionFn = Fn + "." + description;

            public static class releasegroup
            {
                public const string N = nameof(releasegroup);
                public const string Fn = anidb.Fn + "." + N;

                public const string name = nameof(name);
                public const string nameFn = Fn + "." + name;
                public const string shortname = nameof(shortname);
                public const string shortnameFn = Fn + "." + shortname;
            }

            public static class media
            {
                public const string N = nameof(media);
                public const string Fn = anidb.Fn + "." + N;

                public const string sublanguages = nameof(sublanguages);
                public const string sublanguagesFn = Fn + "." + sublanguages;
                public const string dublanguages = nameof(dublanguages);
                public const string dublanguagesFn = Fn + "." + dublanguages;
            }
        }
    }
}
