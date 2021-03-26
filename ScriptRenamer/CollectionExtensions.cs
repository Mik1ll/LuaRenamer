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
                IEnumerable<string> s => s.DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                IEnumerable<TitleLanguage> t => t.Select(t => t.ToString()).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                IEnumerable<IImportFolder> i => i.Select(f => f.Location).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                IEnumerable<AnimeTitle> t => t.Select(t => t.Title).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
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
                _ => throw new KeyNotFoundException("Could not find collection type in ToList")
            };
        }
    }
}
