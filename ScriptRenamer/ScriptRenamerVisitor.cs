using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
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
            return (context.AUDIOCODECS()?.Symbol.Type ?? context.LANGUAGE_ENUM()?.Symbol.Type ?? context.IMPORTFOLDERS()?.Symbol.Type ?? context.title_collection_expr() ?? context.collection_labels() ?? (object)context.FIRST().Symbol.Type) switch
            {
                SRP.AUDIOCODECS => ((List<string>)GetCollection(context.AUDIOCODECS().Symbol.Type)).Where(c => c.Contains(rhsString)).ToList(),
                SRP.LANGUAGE_ENUM => ((List<TitleLanguage>)GetCollection(context.langs.Type))
                                     .Where(l => l == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText())).ToList(),
                SRP.IMPORTFOLDERS => ((List<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type))
                                     .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals(rhsString)).ToList(),
                SRP.Title_collection_exprContext => (IList)Visit(context.title_collection_expr()),
                SRP.Collection_labelsContext => (IList)Visit(context.collection_labels()),
                SRP.FIRST => ((IList)Visit(context.collection_expr())).Take(1).ToList(),
                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
            };
        }

        public override IList VisitTitle_collection_expr([NotNull] SRP.Title_collection_exprContext context)
        {
            Func<AnimeTitle, bool> wherePred = context.rhs?.Type switch
            {
                SRP.TITLETYPE_ENUM => at => at.Type == ParseEnum<TitleType>(context.TITLETYPE_ENUM().GetText()),
                SRP.LANGUAGE_ENUM => at => at.Language == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText()),
                _ => throw new ParseCanceledException("Could not parse title_collection_expr right hand side", context.exception),
            };
            return (context.title_collection_expr() ?? (object)context.lhs?.Type) switch
            {
                SRP.Title_collection_exprContext => ((List<AnimeTitle>)Visit(context.title_collection_expr())).Where(wherePred).ToList(),
                SRP.ANIMETITLES or SRP.EPISODETITLES => ((List<AnimeTitle>)GetCollection(context.lhs.Type)).Where(wherePred).ToList(),
                _ => throw new ParseCanceledException("Could not parse title_collection_expr", context.exception),
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
                SRP.GROUPSHORT => FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName,
                SRP.GROUPLONG => FileInfo.AniDBFileInfo?.ReleaseGroup?.Name,
                SRP.CRCLOWER => FileInfo.Hashes.CRC.ToLower(),
                SRP.CRCUPPER => FileInfo.Hashes.CRC.ToUpper(),
                SRP.SOURCE => FileInfo.AniDBFileInfo?.Source,
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
                SRP.VIDEOCODECLONG => FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec ?? FileInfo.MediaInfo?.Video?.Codec,
                SRP.VIDEOCODECSHORT => FileInfo.MediaInfo?.Video?.SimplifiedCodec,
                SRP.DURATION => FileInfo.MediaInfo?.General?.Duration.ToString(),
                SRP.GROUPNAME => GroupInfo?.Name,
                SRP.OLDFILENAME => FileInfo.Filename,
                SRP.ORIGINALFILENAME => FileInfo.AniDBFileInfo?.OriginalFilename,
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
                SRP.VERSION => FileInfo.AniDBFileInfo?.Version ?? 0,
                SRP.WIDTH => FileInfo.MediaInfo?.Video?.Width ?? 0,
                SRP.HEIGHT => FileInfo.MediaInfo?.Video?.Height ?? 0,
                SRP.EPISODECOUNT => AnimeInfo.EpisodeCounts.Episodes,
                SRP.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth ?? 0,
                SRP.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio?.Select(a => a.Channels).Max() ?? 0,
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
                return context.cancel.Type == SRP.CANCEL
                        || (context.cancel.Type == SRP.CANCELMOVE && !Renaming)
                        || (context.cancel.Type == SRP.CANCELRENAME && Renaming)
                    ? throw new CancelStmtException()
                    : null;
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
                    tar = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                    break;
                case SRP.ADD:
                    tar += context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                    break;
                case SRP.REPLACE:
                    tar = tar.Replace((string)Visit(context.string_atom(0)), (string)Visit(context.string_atom(1)));
                    break;
                default:
                    throw new ParseCanceledException("Could not parse action statement", context.exception);
            }
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
                SRP.IMPORTFOLDERS => AvailableFolders,
                _ => throw new KeyNotFoundException("Could not find token type for collection"),
            };
        }

        private static T ParseEnum<T>(string text) => (T)Enum.Parse(typeof(T), text);

        #endregion utility
    }
}
