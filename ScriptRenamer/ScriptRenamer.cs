using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Antlr4.Runtime;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    [Renamer("ScriptRenamer")]
    class ScriptRenamer : IRenamer
    {
        static ScriptRenamer()
        {
            EmbedDll();
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Script?.Script))
            {
                return (null, null);
            }
            var context = GetContext(args.Script?.Script);
            var visitor = new ScriptRenamerVisitor
            {
                AvailableFolders = args.AvailableFolders ?? new List<IImportFolder>(),
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault()
            };
            _ = visitor.Visit(context);
            return (args.AvailableFolders.FirstOrDefault(f => f.Name == visitor.Destination),
                    !string.IsNullOrWhiteSpace(visitor.Subfolder) ? visitor.Subfolder.ReplaceInvalidPathCharacters() : null);
        }

        public string GetFilename(RenameEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Script?.Script))
            {
                return null;
            }
            var context= GetContext(args.Script.Script);
            var visitor = new ScriptRenamerVisitor
            {
                AvailableFolders = new List<IImportFolder>(),
                AnimeInfo = args.AnimeInfo.FirstOrDefault(),
                EpisodeInfo = args.EpisodeInfo.FirstOrDefault(),
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo.FirstOrDefault()
            };
            _ = visitor.Visit(context);
            return !string.IsNullOrWhiteSpace(visitor.Filename) ? visitor.Filename.ReplaceInvalidPathCharacters() + Path.GetExtension(args.FileInfo.Filename) : null;
        }

        private static void EmbedDll()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (new AssemblyName(args.Name).Name != "Antlr4.Runtime.Standard")
                    return null;
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Antlr4.Runtime.Standard.dll";
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    byte[] assemblyData = new byte[stream.Length];
                    _ = stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }

        private static ParserRuleContext GetContext(string script)
        {
            AntlrInputStream inputStream = new(new StringReader(script));
            ScriptRenamerLexer lexer = new(inputStream);
            CommonTokenStream tokenStream = new(lexer);
            ScriptRenamerParser parser = new(tokenStream);
            return parser.start();
        }
    }
}
