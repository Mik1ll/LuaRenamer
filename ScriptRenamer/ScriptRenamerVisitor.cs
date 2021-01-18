using System.Collections.Generic;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    public class ScriptRenamerVisitor : ScriptRenamerBaseVisitor<object>
    {
        public string filename = string.Empty;
        public string destination = string.Empty;
        public string subfolder = string.Empty;

        public List<IImportFolder> AvailableFolders { get; set; }
        public IVideoFile FileInfo { get; set; }
        public IAnime AnimeInfo { get; set; }
        public IGroup GroupInfo { get; set; }
        public IEpisode EpisodeInfo { get; set; }
        public IRenameScript Script { get; set; }



    }
}