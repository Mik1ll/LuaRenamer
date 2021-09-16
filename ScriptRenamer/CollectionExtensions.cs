using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    public static class CollectionExtensions
    {
        public static string CollectionString(this IEnumerable myenum)
        {
            return myenum switch
            {
                IEnumerable<string> s => string.Join(", ", s),
                IEnumerable<TitleLanguage> t => string.Join(", ", t),
                IEnumerable<IImportFolder> i => string.Join(", ", i.Select(f => f.Location)),
                IEnumerable<AnimeTitle> t => string.Join(", ", t.Select(tt => tt.Title)),
                IEnumerable<int> i => string.Join(", ", i),
                IEnumerable<IAudioStream> a => string.Join(", ", a.Select(b => $"{b.LanguageCode} {b.SimplifiedCodec} {b.Channels}")),
                _ => throw new KeyNotFoundException("Could not find collection type in CollectionString")
            };
        }

        public static IEnumerable Take(this IEnumerable myenum, int i)
        {
            return myenum switch
            {
                IEnumerable<string> c => Enumerable.Take(c, i),
                IEnumerable<AnimeTitle> c => Enumerable.Take(c, i),
                IEnumerable<TitleLanguage> c => Enumerable.Take(c, i),
                IEnumerable<IImportFolder> c => Enumerable.Take(c, i),
                IEnumerable<int> c => Enumerable.Take(c, i),
                IEnumerable<IAudioStream> c => Enumerable.Take(c, i),
                _ => throw new KeyNotFoundException("Could not find collection type in Take")
            };
        }

        public static IList ToList(this IEnumerable myenum)
        {
            return myenum switch
            {
                IEnumerable<string> c => Enumerable.ToList(c),
                IEnumerable<AnimeTitle> c => Enumerable.ToList(c),
                IEnumerable<TitleLanguage> c => Enumerable.ToList(c),
                IEnumerable<IImportFolder> c => Enumerable.ToList(c),
                IEnumerable<int> c => Enumerable.ToList(c),
                IEnumerable<IAudioStream> c => Enumerable.ToList(c),
                _ => throw new KeyNotFoundException("Could not find collection type in ToList")
            };
        }
    }
}
