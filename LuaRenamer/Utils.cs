using System;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions;

namespace LuaRenamer
{
    public static class Utils
    {
        public static string NormPath(string path)
        {
            return path?.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        }

        public static string RemoveInvalidFilenameChars(string filename)
        {
            filename = filename.RemoveInvalidPathCharacters();
            filename = string.Concat(filename.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            return filename;
        }

        public static T ParseEnum<T>(string text, bool throwException = true)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), string.Concat(text.Where(c => !char.IsWhiteSpace(c))), true);
            }
            catch
            {
                if (throwException)
                    throw;
                return default;
            }
        }

        public static Type GetTypeFromAssemblies(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(currentassembly => currentassembly.GetType(typeName, false, true))
                .FirstOrDefault(t => t is not null);
        }
    }
}
