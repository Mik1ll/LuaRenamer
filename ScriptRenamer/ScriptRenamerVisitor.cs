using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using SRP = ScriptRenamerParser;

namespace ScriptRenamer
{
    public class ScriptRenamerVisitor : ScriptRenamerBaseVisitor<object>
    {
        public string Filename;
        public string Destination;
        public string Subfolder;

        public bool Renaming { get; set; } = true;

        public List<IImportFolder> AvailableFolders { get; set; } = new List<IImportFolder>();
        public IVideoFile FileInfo { get; set; }
        public IAnime AnimeInfo { get; set; }
        public IGroup GroupInfo { get; set; }
        public IEpisode EpisodeInfo { get; set; }
        public IRenameScript Script { get; set; }
        
        private int LastEpisodeNumber { get; set; }

        public ScriptRenamerVisitor()
        {
        }

        public ScriptRenamerVisitor(RenameEventArgs args)
        {
            Renaming = true;
            AvailableFolders = new List<IImportFolder>();
            AnimeInfo = args.AnimeInfo.FirstOrDefault();
            EpisodeInfo = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID).OrderBy(e => e.Number).ThenBy(e => e.Type).FirstOrDefault();
            var seq = EpisodeInfo.Number - 1;
            LastEpisodeNumber = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID && e.Type == EpisodeInfo?.Type)
                                                .OrderBy(e => e.Number).LastOrDefault(e => e.Number <= (seq += 1))?.Number ?? 0;
            FileInfo = args.FileInfo;
            GroupInfo = args.GroupInfo.FirstOrDefault();
            Script = args.Script;
        }

        public ScriptRenamerVisitor(MoveEventArgs args)
        {
            Renaming = false;
            AvailableFolders = args.AvailableFolders;
            AnimeInfo = args.AnimeInfo.FirstOrDefault();
            EpisodeInfo = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID).OrderBy(e => e.Number).ThenBy(e => e.Type).FirstOrDefault();
            var seq = EpisodeInfo.Number - 1;
            LastEpisodeNumber = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID && e.Type == EpisodeInfo?.Type)
                                                .OrderBy(e => e.Number).LastOrDefault(e => e.Number <= (seq += 1))?.Number ?? 0;
            FileInfo = args.FileInfo;
            GroupInfo = args.GroupInfo.FirstOrDefault();
            Script = args.Script;
        }

        #region expressions

        public override object VisitBool_expr([NotNull] SRP.Bool_exprContext context)
        {
            return (context.op?.Type) switch
            {
                SRP.NOT => !(bool)Visit(context.bool_expr(0)),
                SRP.IS => context.is_left.Type switch
                {
                    SRP.ANIMETYPE => AnimeInfo.Type == ParseEnum<AnimeType>(context.ANIMETYPE_ENUM().GetText()),
                    SRP.EPISODETYPE => EpisodeInfo.Type == ParseEnum<EpisodeType>(context.EPISODETYPE_ENUM().GetText()),
                    _ => throw new ParseCanceledException("Could not find matching operands for bool_expr IS", context.exception),
                },
                SRP.GT => (int)Visit(context.number_atom(0)) > (int)Visit(context.number_atom(1)),
                SRP.GE => (int)Visit(context.number_atom(0)) >= (int)Visit(context.number_atom(1)),
                SRP.LT => (int)Visit(context.number_atom(0)) < (int)Visit(context.number_atom(1)),
                SRP.LE => (int)Visit(context.number_atom(0)) <= (int)Visit(context.number_atom(1)),
                SRP.EQ => (context.bool_expr(0) ?? context.number_atom(0) ?? (object)context.string_atom(0)) switch
                {
                    SRP.Number_atomContext => Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    SRP.String_atomContext => Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    SRP.Bool_exprContext => (bool)Visit(context.bool_expr(0)) == (bool)Visit(context.bool_expr(1)),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ", context.exception),
                },
                SRP.NE => (context.bool_expr(0) ?? context.number_atom(0) ?? (object)context.string_atom(0)) switch
                {
                    SRP.Number_atomContext => !Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    SRP.String_atomContext => !Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    SRP.Bool_exprContext => (bool)Visit(context.bool_expr(0)) != (bool)Visit(context.bool_expr(1)),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr NE", context.exception),
                },
                SRP.AND => (bool)Visit(context.bool_expr(0)) && (bool)Visit(context.bool_expr(1)),
                SRP.OR => (bool)Visit(context.bool_expr(0)) || (bool)Visit(context.bool_expr(1)),
                SRP.LPAREN => (bool)Visit(context.bool_expr(0)),
                SRP.CONTAINS => ((string)Visit(context.string_atom(0))).Contains((string)Visit(context.string_atom(1))),
                null => (context.bool_atom() ?? (object)context.collection_expr()) switch
                {
                    SRP.Bool_atomContext => (bool)Visit(context.bool_atom()),
                    SRP.Collection_exprContext => ((IList)Visit(context.collection_expr())).Count > 0,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in bool_expr NE", context.exception),
                },
                _ => throw new ParseCanceledException("Could not parse bool_expr", context.exception),
            };
        }

        public override IList VisitCollection_expr([NotNull] SRP.Collection_exprContext context)
        {
            string rhsString = context.string_atom() is not null ? (string)Visit(context.string_atom()) : string.Empty;
            return (context.AUDIOCODECS()?.Symbol.Type ?? context.langs?.Type ?? context.IMPORTFOLDERS()?.Symbol.Type ?? context.collection_labels() ?? context.titles?.Type ?? (object)context.FIRST().Symbol.Type) switch
            {
                SRP.AUDIOCODECS => ((List<string>)GetCollection(context.AUDIOCODECS().Symbol.Type)).Where(c => c.Contains(rhsString)).ToList(),
                SRP.SUBLANGUAGES or SRP.DUBLANGUAGES => ((List<TitleLanguage>)GetCollection(context.langs.Type))
                                     .Where(l => l == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText())).ToList(),
                SRP.IMPORTFOLDERS => ((List<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type)).Where(f =>
                    (string.Equals(f.Name, rhsString, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(ScriptRenamer.NormPath(f.Location), ScriptRenamer.NormPath(rhsString), StringComparison.OrdinalIgnoreCase)
                    ) && f.DropFolderType.HasFlag(DropFolderType.Destination)).ToList(),
                SRP.ANIMETITLES or SRP.EPISODETITLES => ((List<AnimeTitle>)GetCollection(context.titles.Type))
                                .Where(at => context.t is null || at.Type == ParseEnum<TitleType>(context.t.Text))
                                .Where(at => context.l is null || at.Language == ParseEnum<TitleLanguage>(context.l.Text)).ToList(),
                SRP.Collection_labelsContext => (IList)Visit(context.collection_labels()),
                SRP.FIRST => ((IList)Visit(context.collection_expr())).Take(1).ToList(),
                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
            };
        }

        #endregion expressions

        #region labels

        public override object VisitBool_labels([NotNull] SRP.Bool_labelsContext context)
        {
            return context.label.Type switch
            {
                SRP.RESTRICTED => AnimeInfo.Restricted,
                SRP.CENSORED => FileInfo.AniDBFileInfo?.Censored ?? false,
                SRP.CHAPTERED => FileInfo.MediaInfo?.Chaptered ?? false,
                SRP.MANUALLYLINKED => FileInfo.AniDBFileInfo is null,
                SRP.INDROPSOURCE => OldDestination()?.DropFolderType.HasFlag(DropFolderType.Source) ?? false,
                _ => throw new ParseCanceledException("Could not parse bool_labels", context.exception),
            };
        }

        public override object VisitString_labels([NotNull] SRP.String_labelsContext context)
        {
            return context.label.Type switch
            {
                SRP.ANIMETITLEPREFERRED => AnimeInfo.PreferredTitle,
                SRP.ANIMETITLEROMAJI => AnimeTitleLanguage(TitleLanguage.Romaji),
                SRP.ANIMETITLEENGLISH => AnimeTitleLanguage(TitleLanguage.English),
                SRP.ANIMETITLEJAPANESE => AnimeTitleLanguage(TitleLanguage.Japanese),
                SRP.EPISODETITLEROMAJI => EpisodeTitleLanguage(TitleLanguage.Romaji),
                SRP.EPISODETITLEENGLISH => EpisodeTitleLanguage(TitleLanguage.English),
                SRP.EPISODETITLEJAPANESE => EpisodeTitleLanguage(TitleLanguage.Japanese),
                SRP.GROUPSHORT => new[] { "raw", "unknown" }.Any(s => FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName.Contains(s, StringComparison.OrdinalIgnoreCase) ?? true)
                                                            ? null : FileInfo.AniDBFileInfo.ReleaseGroup.ShortName,
                SRP.GROUPLONG => new[] { "raw", "unknown" }.Any(s => FileInfo.AniDBFileInfo?.ReleaseGroup?.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ?? true)
                                                            ? null : FileInfo.AniDBFileInfo.ReleaseGroup.Name,
                SRP.CRCLOWER => FileInfo.Hashes.CRC.ToLower(),
                SRP.CRCUPPER => FileInfo.Hashes.CRC.ToUpper(),
                SRP.SOURCE => FileInfo.AniDBFileInfo?.Source.Contains("unknown", StringComparison.OrdinalIgnoreCase) ?? true ? null : FileInfo.AniDBFileInfo.Source,
                SRP.RESOLUTION => FileInfo.MediaInfo?.Video?.StandardizedResolution,
                SRP.ANIMETYPE => AnimeInfo.Type.ToString(),
                SRP.EPISODETYPE => EpisodeInfo.Type.ToString(),
                SRP.EPISODEPREFIX => EpisodeInfo.Type switch
                {
                    EpisodeType.Episode => "",
                    EpisodeType.Special => "S",
                    EpisodeType.Credits => "C",
                    EpisodeType.Trailer => "T",
                    EpisodeType.Parody => "P",
                    EpisodeType.Other => "O",
                    _ => ""
                },
                SRP.VIDEOCODECLONG => FileInfo.MediaInfo?.Video?.CodecID,
                SRP.VIDEOCODECSHORT => FileInfo.MediaInfo?.Video?.SimplifiedCodec,
                SRP.VIDEOCODECANIDB => FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec,
                SRP.DURATION => FileInfo.MediaInfo?.General?.Duration.ToString(),
                SRP.GROUPNAME => GroupInfo?.Name,
                SRP.OLDFILENAME => FileInfo.Filename,
                SRP.ORIGINALFILENAME => FileInfo.AniDBFileInfo?.OriginalFilename,
                SRP.OLDIMPORTFOLDER => OldDestination()?.Location,
                _ => throw new ParseCanceledException("Could not parse string_labels", context.exception),
            };
        }

        public override object VisitCollection_labels([NotNull] SRP.Collection_labelsContext context)
        {
            return context.label.Type switch
            {
                SRP.AUDIOCODECS => GetCollection(context.AUDIOCODECS().Symbol.Type),
                SRP.DUBLANGUAGES => GetCollection(context.DUBLANGUAGES().Symbol.Type),
                SRP.SUBLANGUAGES => GetCollection(context.SUBLANGUAGES().Symbol.Type),
                SRP.ANIMETITLES => GetCollection(context.ANIMETITLES().Symbol.Type),
                SRP.EPISODETITLES => GetCollection(context.EPISODETITLES().Symbol.Type),
                SRP.IMPORTFOLDERS => GetCollection(context.IMPORTFOLDERS().Symbol.Type),
                _ => throw new ParseCanceledException("Could not parse collection labels", context.exception),
            };
        }

        public override object VisitNumber_labels([NotNull] SRP.Number_labelsContext context)
        {
            return context.label.Type switch
            {
                SRP.ANIMEID => AnimeInfo.AnimeID,
                SRP.EPISODEID => EpisodeInfo.EpisodeID,
                SRP.EPISODENUMBER => EpisodeInfo.Number,
                SRP.VERSION => FileInfo.AniDBFileInfo?.Version ?? 1,
                SRP.WIDTH => FileInfo.MediaInfo?.Video?.Width ?? 0,
                SRP.HEIGHT => FileInfo.MediaInfo?.Video?.Height ?? 0,
                SRP.EPISODECOUNT => AnimeInfo.EpisodeCounts.Episodes,
                SRP.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth ?? 0,
                SRP.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio?.Select(a => a.Channels).Max() ?? 0,
                SRP.SERIESINGROUP => GroupInfo?.Series.Count ?? 1,
                SRP.LASTEPISODENUMBER => LastEpisodeNumber,
                _ => throw new ParseCanceledException("Could not parse number_labels", context.exception),
            };
        }

        #endregion labels

        #region atoms

        public override object VisitBool_atom([NotNull] SRP.Bool_atomContext context)
        {
            return (context.string_atom() ?? context.bool_labels() ?? context.number_atom() ?? (object)context.BOOLEAN()?.Symbol.Type) switch
            {
                SRP.String_atomContext => !string.IsNullOrEmpty((string)Visit(context.string_atom())),
                SRP.Bool_labelsContext => Visit(context.bool_labels()),
                SRP.Number_atomContext => (int)Visit(context.number_atom()) != 0,
                SRP.BOOLEAN => bool.Parse(context.BOOLEAN().GetText()),
                _ => throw new ParseCanceledException("Could not parse bool_atom", context.exception),
            };
        }

        public override object VisitString_atom([NotNull] SRP.String_atomContext context)
        {
            return (context.number_atom() ?? context.string_labels() ?? context.STRING()?.Symbol.Type ?? context.date_atom() ?? (object)context.collection_expr()) switch
            {
                SRP.Number_atomContext => Visit(context.number_atom()).ToString(),

                SRP.String_labelsContext => Visit(context.string_labels()),
                SRP.STRING => context.STRING().GetText().Trim(new char[] { '\'', '"' }),
                SRP.Collection_exprContext => ((IList)Visit(context.collection_expr())).CollectionString(),
                SRP.Date_atomContext => Visit(context.date_atom()),
                _ => throw new ParseCanceledException("Could not parse string_atom", context.exception),
            };
        }

        public override object VisitNumber_atom([NotNull] SRP.Number_atomContext context)
        {
            return (context.number_labels() ?? context.collection_expr() ?? context.string_atom() ?? (object)context.NUMBER()?.Symbol.Type) switch
            {
                SRP.Number_labelsContext => Visit(context.number_labels()),
                SRP.Collection_exprContext => ((IList)Visit(context.collection_expr())).Count,
                SRP.String_atomContext => ((string)Visit(context.string_atom())).Length,
                SRP.NUMBER => int.Parse(context.NUMBER().GetText()),
                _ => throw new ParseCanceledException("Could not parse number_atom", context.exception),
            };
        }

        public override object VisitDate_atom([NotNull] SRP.Date_atomContext context)
        {
            var date = context.type.Type switch
            {
                SRP.ANIMERELEASEDATE => AnimeInfo.AirDate,
                SRP.EPISDOERELEASEDATE => EpisodeInfo.AirDate,
                SRP.FILERELEASEDATE => FileInfo.AniDBFileInfo?.ReleaseDate,
                _ => throw new ParseCanceledException("Could not parse date_atom", context.exception),
            };
            if (context.DOT() is not null)
            {
                return context.field.Type switch
                {
                    SRP.YEAR => date?.Year.ToString(),
                    SRP.MONTH => date?.Month.ToString(),
                    SRP.DAY => date?.Day.ToString(),
                    _ => throw new ParseCanceledException("Could not parse date_atom DOT", context.exception),
                };
            }
            else
            {
                return date?.ToString("yyyy.MM.dd");
            }
        }

        #endregion atoms

        #region statements

        public override object VisitIf_stmt([NotNull] SRP.If_stmtContext context)
        {
            bool result = (bool)Visit(context.bool_expr());
            if (result)
                _ = Visit(context.true_branch);
            else if (context.false_branch is not null)
                _ = Visit(context.false_branch);
            return null;
        }

        public override object VisitStmt([NotNull] SRP.StmtContext context)
        {
            if (context.if_stmt() is not null)
                return Visit(context.if_stmt());
            else if (context.block() is not null)
                return Visit(context.block());
            else if (context.cancel is not null)
                return context.cancel.Type switch
                {
                    SRP.CANCEL => throw new ParseCanceledException($"Line {context.cancel.Line} Column {context.cancel.Column} Cancelled: {context.string_atom()?.Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2)}"),
                    SRP.SKIPRENAME when !Renaming => null,
                    SRP.SKIPMOVE when Renaming => null,
                    SRP.SKIPRENAME when Renaming => throw new SkipException(),
                    SRP.SKIPMOVE when !Renaming => throw new SkipException(),
                    _ => throw new ParseCanceledException("Could not parse skip/cancel", context.exception)
                };
            var target = context.target_labels()?.label.Type;
            if (((target == SRP.DESTINATION || target == SRP.SUBFOLDER) && !Renaming) || ((target == SRP.FILENAME || target == null) && Renaming))
            {
                switch (target)
                {
                    case SRP.FILENAME:
                        DoAction(ref Filename, context);
                        break;
                    case SRP.DESTINATION:
                        DoAction(ref Destination, context);
                        break;
                    case SRP.SUBFOLDER:
                        DoAction(ref Subfolder, context);
                        break;
                    default:
                        DoAction(ref Filename, context);
                        break;
                }
            }
            return null;
        }

        public void DoAction(ref string tar, SRP.StmtContext context)
        {
            switch (context.op.Type)
            {
                case SRP.SET:
                    tar = AggregateString(context);
                    break;
                case SRP.ADD:
                    tar += AggregateString(context);
                    break;
                case SRP.REPLACE:
                    tar = tar.Replace((string)Visit(context.string_atom(0)), (string)Visit(context.string_atom(1)));
                    break;
                default:
                    throw new ParseCanceledException("Could not parse action statement", context.exception);
            }

            string AggregateString(SRP.StmtContext context) => context.string_atom().Select(a => a.STRING() is null || context.target_labels()?.label.Type != SRP.SUBFOLDER ? (string)Visit(a) : ((string)Visit(a)).Replace('/', (char)0x1F).Replace('\\', (char)0x1F)).Aggregate((s1, s2) => s1 + s2);
        }

        #endregion statements

        #region utility

        private string AnimeTitleLanguage(TitleLanguage language)
        {
            var titles = AnimeInfo.Titles.Where(t => t.Language == language);
            return (titles.FirstOrDefault(t => t.Type == TitleType.Main)
                    ?? titles.FirstOrDefault(t => t.Type == TitleType.Official)
                    ?? titles.FirstOrDefault()
                   )?.Title;
        }

        private string EpisodeTitleLanguage(TitleLanguage language)
        {
            var titles = EpisodeInfo.Titles.Where(t => t.Language == language);
            return (titles.FirstOrDefault(t => t.Type == TitleType.Main)
                    ?? titles.FirstOrDefault(t => t.Type == TitleType.Official)
                    ?? titles.FirstOrDefault()
                   )?.Title;
        }

        private IList GetCollection(int tokenType)
        {
            return tokenType switch
            {
                SRP.AUDIOCODECS => FileInfo.AniDBFileInfo?.MediaInfo?.AudioCodecs?.Distinct().ToList()
                                ?? FileInfo.MediaInfo?.Audio?.Select(a => a.SimplifiedCodec).Distinct().ToList()
                                ?? new List<string>(),
                SRP.DUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages?.Distinct().ToList()
                                 ?? FileInfo.MediaInfo?.Audio?.Select(a => ParseEnum<TitleLanguage>(a.LanguageName)).Distinct().ToList()
                                 ?? new List<TitleLanguage>(),
                SRP.SUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.SubLanguages?.Distinct().ToList()
                                 ?? FileInfo.MediaInfo?.Subs?.Select(a => ParseEnum<TitleLanguage>(a.LanguageName)).Distinct().ToList()
                                 ?? new List<TitleLanguage>(),
                SRP.ANIMETITLES => AnimeInfo.Titles.ToList(),
                SRP.EPISODETITLES => EpisodeInfo.Titles.ToList(),
                SRP.IMPORTFOLDERS => AvailableFolders.Where(i => i.DropFolderType.HasFlag(DropFolderType.Destination)).ToList(),
                _ => throw new KeyNotFoundException("Could not find token type for collection"),
            };
        }

        private static T ParseEnum<T>(string text) => (T)Enum.Parse(typeof(T), text);

        private IImportFolder OldDestination() => AvailableFolders.OrderByDescending(f => f.Location.Length)
                                                        .FirstOrDefault(f => $"{ScriptRenamer.NormPath(FileInfo.FilePath)}/".StartsWith($"{ScriptRenamer.NormPath(f.Location)}/", StringComparison.OrdinalIgnoreCase));

        #endregion utility
    }
}
