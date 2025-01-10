// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberHidesStaticFromOuterClass

using System.Runtime.CompilerServices;

#pragma warning disable CS8981
namespace LuaRenamer;

public class LuaEnv : Table
{
    public static readonly LuaEnv Inst = new();
    public string filename => Get();
    public string destination => Get();
    public string subfolder => Get();
    public string use_existing_anime_location => Get();
    public string replace_illegal_chars => Get();
    public string remove_illegal_chars => Get();
    public string skip_rename => Get();
    public string skip_move => Get();
    public string animes => Get();
    public episode episode => new() { Fn = Get() };
    public Array<episode> episodes => new() { Fn = Get() };
    public Array<importfolder> importfolders => new() { Fn = Get() };
    public group group => new() { Fn = Get() };
    public Array<group> groups => new() { Fn = Get() };
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

    public class anime : Table
    {
        public const string N = nameof(anime);
        public const string Fn = N;

        public const string airdate = nameof(airdate);
        public const string airdateFn = Fn + "." + airdate;
        public const string enddate = nameof(enddate);
        public const string enddateFn = Fn + "." + enddate;
        public const string rating = nameof(rating);
        public const string ratingFn = Fn + "." + rating;
        public const string restricted = nameof(restricted);
        public const string restrictedFn = Fn + "." + restricted;
        public const string type = nameof(type);
        public const string typeFn = Fn + "." + type;
        public const string preferredname = nameof(preferredname);
        public const string preferrednameFn = Fn + "." + preferredname;
        public const string defaultname = nameof(defaultname);
        public const string defaultnameFn = Fn + "." + defaultname;
        public const string id = nameof(id);
        public const string idFn = Fn + "." + id;
        public Array<title> titles => new() { Fn = Get() };
        public const string getname = nameof(getname);
        public const string getnameFn = Fn + ":" + getname;
        public const string episodecounts = nameof(episodecounts);
        public const string episodecountsFn = Fn + "." + episodecounts;
        public const string _classid = nameof(_classid);
        public const string _classidFn = Fn + "." + _classid;
        public const string _classidVal = "965AE3D0-CCA2-4179-B3AB-0B4421B2E01D";


        public static class relations
        {
            public const string N = nameof(relations);
            public const string Fn = LuaEnv.anime.Fn + "." + N;

            public const string anime = nameof(anime);
            public const string animeFn = Fn + "." + anime;
            public const string type = nameof(type);
            public const string typeFn = Fn + "." + type;
        }
    }

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
        public importfolder importfolder => new() { Fn = Get() };
        public const string earliestname = nameof(earliestname);
        public const string earliestnameFn = Fn + "." + earliestname;

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

        public static class media
        {
            public const string N = nameof(media);
            public const string Fn = file.Fn + "." + N;

            public const string chaptered = nameof(chaptered);
            public const string chapteredFn = Fn + "." + chaptered;
            public const string duration = nameof(duration);
            public const string durationFn = Fn + "." + duration;
            public const string bitrate = nameof(bitrate);
            public const string bitrateFn = Fn + "." + bitrate;
            public const string sublanguages = nameof(sublanguages);
            public const string sublanguagesFn = Fn + "." + sublanguages;

            public static class video
            {
                public const string N = nameof(video);
                public const string Fn = media.Fn + "." + N;

                public const string height = nameof(height);
                public const string heightFn = Fn + "." + height;
                public const string width = nameof(width);
                public const string widthFn = Fn + "." + width;
                public const string codec = nameof(codec);
                public const string codecFn = Fn + "." + codec;
                public const string res = nameof(res);
                public const string resFn = Fn + "." + res;
                public const string bitrate = nameof(bitrate);
                public const string bitrateFn = Fn + "." + bitrate;
                public const string bitdepth = nameof(bitdepth);
                public const string bitdepthFn = Fn + "." + bitdepth;
                public const string framerate = nameof(framerate);
                public const string framerateFn = Fn + "." + framerate;
            }

            public static class audio
            {
                public const string N = nameof(audio);
                public const string Fn = media.Fn + "." + N;

                public const string compressionmode = nameof(compressionmode);
                public const string compressionmodeFn = Fn + "." + compressionmode;
                public const string channels = nameof(channels);
                public const string channelsFn = Fn + "." + channels;
                public const string samplingrate = nameof(samplingrate);
                public const string samplingrateFn = Fn + "." + samplingrate;
                public const string codec = nameof(codec);
                public const string codecFn = Fn + "." + codec;
                public const string language = nameof(language);
                public const string languageFn = Fn + "." + language;
                public const string title = nameof(title);
                public const string titleFn = Fn + "." + title;
            }
        }
    }
}

public class Table
{
    public string Fn { get; init; } = "";
    public override string ToString() => Fn;
    protected string Get(char sep = '.', [CallerMemberName] string memberName = "") => string.IsNullOrEmpty(Fn) ? memberName : Fn + sep + memberName;
}

public class Array<T> : Table where T : Table, new()
{
    public T this[int index] => new() { Fn = Fn + $"[{index}]" };
}

public class episode : Table
{
    public string duration => Get();
    public string number => Get();
    public string type => Get();
    public string airdate => Get();
    public string animeid => Get();
    public string id => Get();
    public Array<title> titles => new() { Fn = Get() };
    public string getname => Get(':');
    public string prefix => Get();
    public string _classid => Get();
    public const string _classidVal = "02B70716-6350-473A-ADFA-F9746F80CD50";
}

public class importfolder : Table
{
    public string id => Get();
    public string name => Get();
    public string location => Get();
    public string type => Get();
    public string _classid => Get();
    public const string _classidVal = "55138454-4A0D-45EB-8CCE-1CCF00220165";
}

public class group : Table
{
    public string name => Get();
    public string mainanime => Get();
    public string animes => Get();
}

public class title : Table
{
    public string name => Get();
    public string language => Get();
    public string languagecode => Get();
    public string type => Get();
}
