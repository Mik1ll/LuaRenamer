// ReSharper disable InconsistentNaming
// ReSharper disable MemberHidesStaticFromOuterClass

namespace LuaRenamer
{
    public static class LuaEnv
    {
        public const string filename = nameof(filename);
        public const string destination = nameof(destination);
        public const string subfolder = nameof(subfolder);
        public const string use_existing_anime_location = nameof(use_existing_anime_location);
        public const string remove_reserved_chars = nameof(remove_reserved_chars);
        public const string animes = nameof(animes);
        public const string episodes = nameof(episodes);
        public const string importfolders = nameof(importfolders);
        public const string groups = nameof(groups);
        public const string AnimeType = nameof(AnimeType);
        public const string TitleType = nameof(TitleType);
        public const string Language = nameof(Language);
        public const string EpisodeType = nameof(EpisodeType);
        public const string DropFolderType = nameof(DropFolderType);
        public const string episode_numbers = nameof(episode_numbers);

        private static string Fn(string name, string prop = null) => $"{name}{(prop is null ? "" : $".{prop}")}";

        public static class anime
        {
            public const string N = nameof(anime);
            public static string Fn(string prop = null) => LuaEnv.Fn(N, prop);

            public const string airdate = nameof(airdate);
            public const string enddate = nameof(enddate);
            public const string rating = nameof(rating);
            public const string restricted = nameof(restricted);
            public const string type = nameof(type);
            public const string preferredname = nameof(preferredname);
            public const string id = nameof(id);
            public const string titles = nameof(titles);
            public const string getname = nameof(getname);
            public const string episodecounts = nameof(episodecounts);
        }

        public static class title
        {
            public const string name = nameof(name);
            public const string language = nameof(language);
            public const string languagecode = nameof(languagecode);
            public const string type = nameof(type);
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

        public static class file
        {
            public const string N = nameof(file);
            public static string Fn(string prop = null) => LuaEnv.Fn(N, prop);
            public const string name = nameof(name);
            public const string path = nameof(path);
            public const string size = nameof(size);

            public static class hashes
            {
                public const string N = nameof(hashes);
                public static string Fn(string prop = null) => LuaEnv.Fn(file.Fn(N), prop);
                public const string crc = nameof(crc);
                public const string md5 = nameof(md5);
                public const string ed2k = nameof(ed2k);
                public const string sha1 = nameof(sha1);
            }

            public static class anidb
            {
                public const string N = nameof(anidb);
                public static string Fn(string prop = null) => LuaEnv.Fn(file.Fn(N), prop);
                public const string censored = nameof(censored);
                public const string source = nameof(source);
                public const string version = nameof(version);
                public const string releasedate = nameof(releasedate);

                public static class releasegroup
                {
                    public const string N = nameof(releasegroup);
                    public static string Fn(string prop = null) => LuaEnv.Fn(anidb.Fn(N), prop);
                    public const string name = nameof(name);
                    public const string shortname = nameof(shortname);
                }

                public const string id = nameof(id);

                public static class media
                {
                    public const string N = nameof(media);
                    public static string Fn(string prop = null) => LuaEnv.Fn(anidb.Fn(N), prop);
                    public const string videocodec = nameof(videocodec);
                    public const string sublanguages = nameof(sublanguages);
                    public const string dublanguages = nameof(dublanguages);
                }
            }

            public static class media
            {
                public const string N = nameof(media);
                public static string Fn(string prop = null) => LuaEnv.Fn(file.Fn(N), prop);
                public const string chaptered = nameof(chaptered);

                public static class video
                {
                    public const string N = nameof(video);
                    public static string Fn(string prop = null) => LuaEnv.Fn(media.Fn(N), prop);
                    public const string height = nameof(height);
                    public const string width = nameof(width);
                    public const string codec = nameof(codec);
                    public const string res = nameof(res);
                    public const string bitrate = nameof(bitrate);
                    public const string bitdepth = nameof(bitdepth);
                    public const string framerate = nameof(framerate);
                }

                public const string duration = nameof(duration);
                public const string bitrate = nameof(bitrate);
                public const string sublanguages = nameof(sublanguages);

                public static class audio
                {
                    public const string N = nameof(audio);
                    public static string Fn(string prop = null) => LuaEnv.Fn(media.Fn(N), prop);
                    public const string compressionmode = nameof(compressionmode);
                    public const string bitrate = nameof(bitrate);
                    public const string channels = nameof(channels);
                    public const string bitdepth = nameof(bitdepth);
                    public const string samplingrate = nameof(samplingrate);
                    public const string bitratemode = nameof(bitratemode);
                    public const string simplecodec = nameof(simplecodec);
                    public const string codec = nameof(codec);
                    public const string language = nameof(language);
                    public const string title = nameof(title);
                }
            }
        }

        public static class episode
        {
            public const string N = nameof(episode);
            public static string Fn(string prop = null) => LuaEnv.Fn(N, prop);
            public const string duration = nameof(duration);
            public const string number = nameof(number);
            public const string type = nameof(type);
            public const string airdate = nameof(airdate);
            public const string animeid = nameof(animeid);
            public const string id = nameof(id);
            public const string titles = nameof(anime.titles);
            public const string getname = nameof(getname);
            public const string prefix = nameof(prefix);
        }

        public static class importfolder
        {
            public const string name = nameof(name);
            public const string location = nameof(location);
            public const string type = nameof(type);
        }

        public static class group
        {
            public const string name = nameof(name);
            public const string mainSeriesid = nameof(mainSeriesid);
            public const string seriesids = nameof(seriesids);
        }
    }
}
