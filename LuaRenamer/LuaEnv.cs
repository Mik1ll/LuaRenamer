// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberHidesStaticFromOuterClass

namespace LuaRenamer
{
    public static class LuaEnv
    {
        public const string filename = nameof(filename);
        public const string destination = nameof(destination);
        public const string subfolder = nameof(subfolder);
        public const string use_existing_anime_location = nameof(use_existing_anime_location);
        public const string replace_illegal_chars = nameof(replace_illegal_chars);
        public const string remove_illegal_chars = nameof(remove_illegal_chars);
        public const string animes = nameof(animes);
        public const string episodes = nameof(episodes);
        public const string importfolders = nameof(importfolders);
        public const string groups = nameof(groups);
        public const string AnimeType = nameof(AnimeType);
        public const string TitleType = nameof(TitleType);
        public const string Language = nameof(Language);
        public const string EpisodeType = nameof(EpisodeType);
        public const string ImportFolderType = nameof(ImportFolderType);
        public const string episode_numbers = nameof(episode_numbers);
        public const string log = nameof(log);
        public const string logwarn = nameof(logwarn);
        public const string logerror = nameof(logerror);

        public static class anime
        {
            public const string N = nameof(anime);
            public const string airdate = nameof(airdate);
            public const string airdateFn = N + "." + airdate;
            public const string enddate = nameof(enddate);
            public const string enddateFn = N + "." + enddate;
            public const string rating = nameof(rating);
            public const string ratingFn = N + "." + rating;
            public const string restricted = nameof(restricted);
            public const string restrictedFn = N + "." + restricted;
            public const string type = nameof(type);
            public const string typeFn = N + "." + type;
            public const string preferredname = nameof(preferredname);
            public const string preferrednameFn = N + "." + preferredname;
            public const string id = nameof(id);
            public const string idFn = N + "." + id;
            public const string titles = nameof(titles);
            public const string titlesFn = N + "." + titles;
            public const string getname = nameof(getname);
            public const string getnameFn = N + ":" + getname;
            public const string episodecounts = nameof(episodecounts);
            public const string episodecountsFn = N + "." + episodecounts;
            public const string relations = nameof(relations);
            public const string relationsFn = N + "." + relations;
        }

        public static class relatedanime
        {
            public const string anime = nameof(anime);
            public const string relationtype = nameof(relationtype);
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
            public const string name = nameof(name);
            public const string nameFn = N + "." + name;
            public const string path = nameof(path);
            public const string pathFn = N + "." + path;
            public const string size = nameof(size);
            public const string sizeFn = N + "." + size;
            public const string importfolder = nameof(importfolder);

            public static class hashes
            {
                public const string N = nameof(hashes);
                public const string Fn = file.N + "." + N;
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
                public const string Fn = file.N + "." + N;
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
                    public const string Fn = file.anidb.Fn + "." + N;
                    public const string name = nameof(name);
                    public const string nameFn = Fn + "." + name;
                    public const string shortname = nameof(shortname);
                    public const string shortnameFn = Fn + "." + shortname;
                }

                public const string id = nameof(id);
                public const string idFn = Fn + "." + id;

                public static class media
                {
                    public const string N = nameof(media);
                    public const string Fn = file.anidb.Fn + "." + N;
                    public const string sublanguages = nameof(sublanguages);
                    public const string sublanguagesFn = Fn + "." + sublanguages;
                    public const string dublanguages = nameof(dublanguages);
                    public const string dublanguagesFn = Fn + "." + dublanguages;
                }
            }

            public static class media
            {
                public const string N = nameof(media);
                public const string Fn = file.N + "." + N;
                public const string chaptered = nameof(chaptered);
                public const string chapteredFn = Fn + "." + chaptered;

                public static class video
                {
                    public const string N = nameof(video);
                    public const string Fn = file.media.Fn + "." + N;
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

                public const string duration = nameof(duration);
                public const string durationFn = Fn + "." + duration;
                public const string bitrate = nameof(bitrate);
                public const string bitrateFn = Fn + "." + bitrate;
                public const string sublanguages = nameof(sublanguages);
                public const string sublanguagesFn = Fn + "." + sublanguages;

                public static class audio
                {
                    public const string N = nameof(audio);
                    public const string Fn = file.media.Fn + "." + N;
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

        public static class episode
        {
            public const string N = nameof(episode);
            public const string duration = nameof(duration);
            public const string durationFn = N + "." + duration;
            public const string number = nameof(number);
            public const string numberFn = N + "." + number;
            public const string type = nameof(type);
            public const string typeFn = N + "." + type;
            public const string airdate = nameof(airdate);
            public const string airdateFn = N + "." + airdate;
            public const string animeid = nameof(animeid);
            public const string animeidFn = N + "." + animeid;
            public const string id = nameof(id);
            public const string idFn = N + "." + id;
            public const string titles = nameof(anime.titles);
            public const string titlesFn = N + "." + titles;
            public const string getname = nameof(getname);
            public const string getnameFn = N + ":" + getname;
            public const string prefix = nameof(prefix);
            public const string prefixFn = N + "." + prefix;
        }

        public static class importfolder
        {
            public const string name = nameof(name);
            public const string location = nameof(location);
            public const string type = nameof(type);
            public const string _classid = nameof(_classid);
            public const string _index = nameof(_index);
        }

        public static class group
        {
            public const string N = nameof(group);
            public const string name = nameof(name);
            public const string mainseriesid = nameof(mainseriesid);
            public const string seriesids = nameof(seriesids);
        }
    }
}
