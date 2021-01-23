using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using SRP = ScriptRenamerParser;
using SRL = ScriptRenamerLexer;

namespace ScriptRenamer
{
    public class ScriptRenamerVisitor : ScriptRenamerBaseVisitor<object>
    {
        public string Filename { get; set; }
        public string Destination { get; set; }
        public string Subfolder { get; set; }

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
                SRL.NOT => !(bool)Visit(context.bool_expr(0)),
                SRL.IS => context.is_left.Type switch
                {
                    SRL.ANIMETYPE => AnimeInfo.Type == (AnimeType)Enum.Parse(typeof(AnimeType), context.animeType_enum().GetText()),
                    SRL.EPISODETYPE => EpisodeInfo.Type == (EpisodeType)Enum.Parse(typeof(EpisodeType), context.episodeType_enum().GetText()),
                    _ => throw new ParseCanceledException("Could not find matching operands for bool_expr IS", context.exception),
                },
                SRL.GT => (int)Visit(context.number_atom(0)) > (int)Visit(context.number_atom(1)),
                SRL.GE => (int)Visit(context.number_atom(0)) >= (int)Visit(context.number_atom(1)),
                SRL.LT => (int)Visit(context.number_atom(0)) < (int)Visit(context.number_atom(1)),
                SRL.LE => (int)Visit(context.number_atom(0)) <= (int)Visit(context.number_atom(1)),
                SRL.EQ => (context.number_atom(1) ?? (object)context.string_atom(1)) switch
                {
                    SRP.Number_atomContext => Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    SRP.String_atomContext => Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ", context.exception),
                },
                SRL.NE => (context.number_atom(1) ?? (object)context.string_atom(1)) switch
                {
                    SRP.Number_atomContext => !Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    SRP.String_atomContext => !Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr NE", context.exception),
                },
                SRL.AND => (bool)Visit(context.bool_expr(0)) && (bool)Visit(context.bool_expr(1)),
                SRL.OR => (bool)Visit(context.bool_expr(0)) || (bool)Visit(context.bool_expr(1)),
                SRL.LPAREN => (bool)Visit(context.bool_expr(0)),
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
            return (context.AUDIOCODECS()?.Symbol.Type ?? context.langs ?? context.IMPORTFOLDERS()?.Symbol.Type ?? context.title_collection_expr() ?? (object)context.collection_labels()) switch
            {
                SRL.AUDIOCODECS => ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type))
                        .Where(c => c.Contains((string)Visit(context.string_atom()))).ToList(),

                SRL.DUBLANGUAGES or SRL.SUBLANGUAGES => ((ICollection<TitleLanguage>)GetCollection(context.langs.Type))
                        .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList(),

                SRL.IMPORTFOLDERS => ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type))
                        .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals((string)Visit(context.string_atom()))).ToList(),

                SRP.Title_collection_exprContext => ((ICollection<AnimeTitle>)Visit(context.title_collection_expr())).ToList(),

                SRP.Collection_labelsContext => (ICollection)Visit(context.collection_labels()),

                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
            };
        }

        public override object VisitTitle_collection_expr([NotNull] SRP.Title_collection_exprContext context)
        {
            return (context.title_collection_expr() ?? (object)context?.titles.Type) switch
            {
                SRP.Title_collection_exprContext when context.language_enum() is not null => 
                        ((List<AnimeTitle>)Visit(context.title_collection_expr()))
                        .Where(at => at.Language == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().GetText())).ToList(),

                SRP.Title_collection_exprContext when context.titleType_enum() is not null =>
                        ((List<AnimeTitle>)Visit(context.title_collection_expr()))
                        .Where(at => at.Type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList(),

                SRL.ANIMETITLES when context.language_enum() is not null =>
                        ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type))
                        .Where(at => at.Language == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().GetText())).ToList(),

                SRL.ANIMETITLES when context.titleType_enum() is not null =>
                        ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type))
                        .Where(at => at.Type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList(),

                SRL.EPISODETITLES when context.language_enum() is not null =>
                        ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type))
                        .Where(at => at.Language == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().GetText())).ToList(),

                SRL.EPISODETITLES when context.titleType_enum() is not null =>
                        ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type))
                        .Where(at => at.Type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList(),

                _ => throw new ParseCanceledException("Could not parse title_collection_expr", context.exception),
            };
        }
        #endregion expressions

        #region labels
        public override object VisitBool_labels([NotNull] SRP.Bool_labelsContext context)
        {
            return context.label.Type switch
            {
                SRL.RESTRICTED => AnimeInfo.Restricted,
                SRL.CENSORED => FileInfo.AniDBFileInfo?.Censored ?? false,
                SRL.CHAPTERED => FileInfo.MediaInfo?.Chaptered ?? false,
                _ => throw new ParseCanceledException("Could not parse bool_labels", context.exception),
            };
        }

        public override object VisitString_labels([NotNull] SRP.String_labelsContext context)
        {
            return context.label.Type switch
            {
                SRL.ANIMETITLEPREFERRED => AnimeInfo.PreferredTitle,
                SRL.ANIMETITLEROMAJI => AnimeTitleLanguage(TitleLanguage.Romaji),
                SRL.ANIMETITLEENGLISH => AnimeTitleLanguage(TitleLanguage.English),
                SRL.ANIMETITLEJAPANESE => AnimeTitleLanguage(TitleLanguage.Japanese),
                SRL.EPISODETITLEROMAJI => EpisodeTitleLanguage(TitleLanguage.Romaji),
                SRL.EPISODETITLEENGLISH => EpisodeTitleLanguage(TitleLanguage.English),
                SRL.EPISODETITLEJAPANESE => EpisodeTitleLanguage(TitleLanguage.Japanese),
                SRL.GROUPSHORT => FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName,
                SRL.GROUPLONG => FileInfo.AniDBFileInfo?.ReleaseGroup?.Name,
                SRL.CRCLOWER => FileInfo.Hashes.CRC.ToLower(),
                SRL.CRCUPPER => FileInfo.Hashes.CRC.ToUpper(),
                SRL.SOURCE => FileInfo.AniDBFileInfo?.Source,
                SRL.RESOLUTION => FileInfo.MediaInfo?.Video?.StandardizedResolution,
                SRL.ANIMETYPE => AnimeInfo.Type.ToString(),
                SRL.EPISODETYPE => EpisodeInfo.Type.ToString(),
                SRL.EPISODEPREFIX => EpisodeInfo.Type switch
                {
                    EpisodeType.Episode => "",
                    EpisodeType.Special => "S",
                    EpisodeType.Credits => "C",
                    EpisodeType.Trailer => "T",
                    EpisodeType.Parody => "P",
                    EpisodeType.Other => "O",
                    _ => ""
                },
                SRL.VIDEOCODECLONG => FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec ?? FileInfo.MediaInfo?.Video?.Codec,
                SRL.VIDEOCODECSHORT => FileInfo.MediaInfo?.Video?.SimplifiedCodec,
                SRL.DURATION => FileInfo.MediaInfo?.General?.Duration,
                SRL.GROUPNAME => GroupInfo?.Name,
                SRL.OLDFILENAME => FileInfo.Filename,
                SRL.ORIGINALFILENAME => FileInfo.AniDBFileInfo?.OriginalFilename,
                _ => throw new ParseCanceledException("Could not parse string_labels", context.exception),
            };
        }

        public override object VisitCollection_labels([NotNull] SRP.Collection_labelsContext context)
        {
            return context.label.Type switch
            {
                SRL.AUDIOCODECS => (ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type),
                SRL.DUBLANGUAGES => (ICollection<TitleLanguage>)GetCollection(context.DUBLANGUAGES().Symbol.Type),
                SRL.SUBLANGUAGES => (ICollection<TitleLanguage>)GetCollection(context.SUBLANGUAGES().Symbol.Type),
                SRL.ANIMETITLES => (ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type),
                SRL.EPISODETITLES => (ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type),
                SRL.IMPORTFOLDERS => (ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type),
                _ => throw new ParseCanceledException("Could not parse collection labels", context.exception),
            };
        }

        public override object VisitNumber_labels([NotNull] SRP.Number_labelsContext context)
        {
            return context.label.Type switch
            {
                SRL.EPISODENUMBER => EpisodeInfo.Number,
                SRL.FILEVERSION => FileInfo.AniDBFileInfo?.Version,
                SRL.WIDTH => FileInfo.MediaInfo?.Video?.Width,
                SRL.HEIGHT => FileInfo.MediaInfo?.Video.Height,
                SRL.YEAR => AnimeInfo.AirDate?.Year,
                SRL.EPISODECOUNT => AnimeInfo.EpisodeCounts.Episodes,
                SRL.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth,
                SRL.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio.Select(a => a.Channels).Max(),
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
                SRP.Number_atomContext => (int)Visit(context.number_atom()) == 0,
                SRL.BOOLEAN => bool.Parse(context.BOOLEAN().GetText()),
                _ => throw new ParseCanceledException("Could not parse bool_atom", context.exception),
            };
        }

        public override object VisitString_atom([NotNull] SRP.String_atomContext context)
        {
            return (context.number_atom() ?? context.string_labels() ?? context.STRING()?.Symbol.Type ?? context.FIRST()?.Symbol.Type ?? (object)context.collection_expr()) switch
            {
                SRP.Number_atomContext => Visit(context.number_atom()).ToString(),

                SRP.String_labelsContext => Visit(context.string_labels()),
                SRL.STRING => context.STRING().GetText().Trim(new char[] { '\'', '"' }),
                SRP.Collection_exprContext when context.FIRST() is null => ((IEnumerable)Visit(context.collection_expr())).Cast<object>() switch
                {
                    IEnumerable<string> s => s.DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<AnimeTitle> t => t.Select(t => t.Title).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<TitleLanguage> t => t.Select(t => t.ToString()).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    IEnumerable<IImportFolder> i => i.Select(f => f.Name).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}"),
                    _ => throw new ParseCanceledException("Could not parse collection_expr in string_atom", context.exception)
                },
                SRL.FIRST => ((ICollection)Visit(context.collection_expr())).Cast<object>().FirstOrDefault() switch
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
                SRL.NUMBER => int.Parse(context.NUMBER().GetText()),
                _ => throw new ParseCanceledException("Could not parse number_atom", context.exception),
            };
        }
        #endregion atoms

        #region statements
        public override object VisitIf_stmt([NotNull] SRP.If_stmtContext context)
        {
            bool result = (bool)Visit(context.bool_expr());
            if (result)
            {
                _ = Visit(context.true_branch);
            }
            else if (context.false_branch is not null)
            {
                _ = Visit(context.false_branch);
            }
            return null;
        }

        public override object VisitSet_stmt([NotNull] SRP.Set_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == SRL.DESTINATION || target == SRL.SUBFOLDER) ^ Renaming)
            {
                var setstring = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                switch (target)
                {
                    case SRL.FILENAME:
                        Filename = setstring;
                        break;
                    case SRL.DESTINATION:
                        Destination = setstring;
                        break;
                    case SRL.SUBFOLDER:
                        Subfolder = setstring;
                        break;
                    default:
                        Filename = setstring;
                        break;
                }
            }
            return null;
        }

        public override object VisitAdd_stmt([NotNull] SRP.Add_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == SRL.DESTINATION || target == SRL.SUBFOLDER) ^ Renaming)
            {
                var addString = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                switch (target)
                {
                    case SRL.FILENAME:
                        Filename += addString;
                        break;
                    case SRL.DESTINATION:
                        Destination += addString;
                        break;
                    case SRL.SUBFOLDER:
                        Subfolder += addString;
                        break;
                    default:
                        Filename += addString;
                        break;
                }
            }
            return null;
        }

        public override object VisitReplace_stmt([NotNull] SRP.Replace_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == SRL.DESTINATION || target == SRL.SUBFOLDER) ^ Renaming)
            {
                var oldstr = (string)Visit(context.string_atom(0));
                var newstr = (string)Visit(context.string_atom(1));
                switch (target)
                {
                    case SRL.FILENAME:
                        Filename = Filename.Replace(oldstr, newstr);
                        break;
                    case SRL.DESTINATION:
                        Destination = Destination.Replace(oldstr, newstr);
                        break;
                    case SRL.SUBFOLDER:
                        Subfolder = Subfolder.Replace(oldstr, newstr);
                        break;
                    default:
                        Filename = Filename.Replace(oldstr, newstr);
                        break;
                }
            }
            return null;
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
                SRL.AUDIOCODECS => FileInfo.AniDBFileInfo?.MediaInfo?.AudioCodecs.Distinct().ToList()
                                ?? FileInfo.MediaInfo?.Audio.Select(a => a.SimplifiedCodec).Distinct().ToList()
                                ?? new List<string>(),
                SRL.DUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages.Distinct().ToList()
                                 ?? FileInfo.MediaInfo?.Audio.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).Distinct().ToList()
                                 ?? new List<TitleLanguage>(),
                SRL.SUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.SubLanguages.Distinct().ToList()
                                 ?? FileInfo.MediaInfo?.Subs.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).Distinct().ToList()
                                 ?? new List<TitleLanguage>(),
                SRL.ANIMETITLES => AnimeInfo.Titles.ToList(),
                SRL.EPISODETITLES => EpisodeInfo.Titles.ToList(),
                SRL.IMPORTFOLDERS => AvailableFolders,
                _ => throw new KeyNotFoundException("Could not find token type for collection"),
            };
        }
        #endregion utility
    }
}