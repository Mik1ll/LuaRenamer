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
                    SRP.Collection_exprContext => ((ICollection)Visit(context.collection_expr())).Count > 0,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in bool_expr NE", context.exception),
                },
                _ => throw new ParseCanceledException("Could not parse bool_expr", context.exception),
            };
        }

        public override object VisitCollection_expr([NotNull] SRP.Collection_exprContext context)
        {
            return (context.AUDIOCODECS()?.Symbol.Type ?? context.LANGUAGE_ENUM()?.Symbol.Type ?? context.IMPORTFOLDERS()?.Symbol.Type ?? context.title_collection_expr() ?? (object)context.collection_labels()) switch
            {
                SRP.AUDIOCODECS => ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type))
                        .Where(c => c.Contains((string)Visit(context.string_atom()))).ToList(),

                SRP.LANGUAGE_ENUM => ((ICollection<TitleLanguage>)GetCollection(context.langs.Type))
                        .Where(l => l == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText())).ToList(),

                SRP.IMPORTFOLDERS => ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type))
                        .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals((string)Visit(context.string_atom()))).ToList(),

                SRP.Title_collection_exprContext => ((ICollection<AnimeTitle>)Visit(context.title_collection_expr())).ToList(),

                SRP.Collection_labelsContext => (ICollection)Visit(context.collection_labels()),

                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
            };
        }

        public override object VisitTitle_collection_expr([NotNull] SRP.Title_collection_exprContext context)
        {
            Func<AnimeTitle, bool> wherePred = context.rhs?.Type switch
            {
                SRP.TITLETYPE_ENUM => at => at.Type == ParseEnum<TitleType>(context.TITLETYPE_ENUM().GetText()),
                SRP.LANGUAGE_ENUM => at => at.Language == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText()),
                _ => throw new ParseCanceledException("Could not parse title_collection_expr right hand side", context.exception),
            };
            return (context.title_collection_expr() ?? (object)context.lhs?.Type) switch
            {
                SRP.Title_collection_exprContext => ((ICollection<AnimeTitle>)Visit(context.title_collection_expr())).Where(wherePred).ToList(),
                SRP.ANIMETITLES or SRP.EPISODETITLES => ((ICollection<AnimeTitle>)GetCollection(context.lhs.Type)).Where(wherePred).ToList(),
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
                SRP.DURATION => FileInfo.MediaInfo?.General?.Duration,
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
                SRP.AUDIOCODECS => (ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type),
                SRP.DUBLANGUAGES => (ICollection<TitleLanguage>)GetCollection(context.DUBLANGUAGES().Symbol.Type),
                SRP.SUBLANGUAGES => (ICollection<TitleLanguage>)GetCollection(context.SUBLANGUAGES().Symbol.Type),
                SRP.ANIMETITLES => (ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type),
                SRP.EPISODETITLES => (ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type),
                SRP.IMPORTFOLDERS => (ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type),
                _ => throw new ParseCanceledException("Could not parse collection labels", context.exception),
            };
        }

        public override object VisitNumber_labels([NotNull] SRP.Number_labelsContext context)
        {
            return context.label.Type switch
            {
                SRP.EPISODENUMBER => EpisodeInfo.Number,
                SRP.FILEVERSION => FileInfo.AniDBFileInfo?.Version,
                SRP.WIDTH => FileInfo.MediaInfo?.Video?.Width,
                SRP.HEIGHT => FileInfo.MediaInfo?.Video?.Height,
                SRP.YEAR => AnimeInfo.AirDate?.Year,
                SRP.EPISODECOUNT => AnimeInfo.EpisodeCounts.Episodes,
                SRP.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth,
                SRP.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio?.Select(a => a.Channels).Max(),
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
            return (context.number_atom() ?? context.string_labels() ?? context.STRING()?.Symbol.Type ?? context.FIRST()?.Symbol.Type ?? (object)context.collection_expr()) switch
            {
                SRP.Number_atomContext => Visit(context.number_atom()).ToString(),

                SRP.String_labelsContext => Visit(context.string_labels()),
                SRP.STRING => context.STRING().GetText().Trim(new char[] { '\'', '"' }),
                SRP.Collection_exprContext when context.FIRST() is null => ((IEnumerable)Visit(context.collection_expr())).Cast<object>() switch
                {
                    IEnumerable<string> s => s.DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<AnimeTitle> t => t.Select(t => t.Title).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<TitleLanguage> t => t.Select(t => t.ToString()).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<IImportFolder> i => i.Select(f => f.Name).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    _ => throw new ParseCanceledException("Could not parse collection_expr in string_atom", context.exception)
                },
                SRP.FIRST => ((ICollection)Visit(context.collection_expr())).Cast<object>().FirstOrDefault() switch
                {
                    string s => s,
                    AnimeTitle t => t.Title,
                    TitleLanguage t => t.ToString(),
                    IImportFolder i => i.Name,
                    null => string.Empty,
                    _ => throw new ParseCanceledException("Could not parse first_collection_expr in string_atom"),
                },
                _ => throw new ParseCanceledException("Could not parse string_atom", context.exception),
            };
        }

        public override object VisitNumber_atom([NotNull] SRP.Number_atomContext context)
        {
            return (context.number_labels() ?? context.collection_expr() ?? context.string_atom() ?? (object)context.NUMBER()?.Symbol.Type) switch
            {
                SRP.Number_labelsContext => Visit(context.number_labels()),
                SRP.Collection_exprContext => ((ICollection)Visit(context.collection_expr())).Count,
                SRP.String_atomContext => ((string)Visit(context.string_atom())).Length,
                SRP.NUMBER => int.Parse(context.NUMBER().GetText()),
                _ => throw new ParseCanceledException("Could not parse number_atom", context.exception),
            };
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
            var target = context.target_labels()?.label.Type;
            if ((target == SRP.DESTINATION || target == SRP.SUBFOLDER) ^ Renaming)
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

        private ICollection GetCollection(int tokenType)
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