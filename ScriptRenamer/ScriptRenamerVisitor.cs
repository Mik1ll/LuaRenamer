using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace ScriptRenamer
{
    public class ScriptRenamerVisitor : ScriptRenamerBaseVisitor<object>
    {
        public string Filename { get; set; }
        public string Destination { get; set; }
        public string Subfolder { get; set; }

        public bool Renaming { get; set; }

        public List<IImportFolder> AvailableFolders { get; set; } = new List<IImportFolder>();
        public IVideoFile FileInfo { get; set; }
        public IAnime AnimeInfo { get; set; }
        public IGroup GroupInfo { get; set; }
        public IEpisode EpisodeInfo { get; set; }

        #region expressions
        public override object VisitBool_expr([NotNull] ScriptRenamerParser.Bool_exprContext context)
        {
            return (context.op?.Type) switch
            {
                ScriptRenamerLexer.NOT => !(bool)Visit(context.bool_expr(0)),
                ScriptRenamerLexer.IS => context.is_left.Type switch
                {
                    ScriptRenamerLexer.ANIMETYPE => AnimeInfo.Type == (AnimeType)Enum.Parse(typeof(AnimeType), context.animeType_enum().GetText()),
                    ScriptRenamerLexer.EPISODETYPE => EpisodeInfo.Type == (EpisodeType)Enum.Parse(typeof(EpisodeType), context.episodeType_enum().GetText()),
                    _ => throw new ParseCanceledException("Could not find matching operands for bool_expr IS", context.exception),
                },
                ScriptRenamerLexer.GT => (int)Visit(context.number_atom(0)) > (int)Visit(context.number_atom(1)),
                ScriptRenamerLexer.GE => (int)Visit(context.number_atom(0)) >= (int)Visit(context.number_atom(1)),
                ScriptRenamerLexer.LT => (int)Visit(context.number_atom(0)) < (int)Visit(context.number_atom(1)),
                ScriptRenamerLexer.LE => (int)Visit(context.number_atom(0)) <= (int)Visit(context.number_atom(1)),
                ScriptRenamerLexer.EQ => (context.number_atom(1) ?? (object)context.string_atom(1)) switch
                {
                    ScriptRenamerParser.Number_atomContext => Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    ScriptRenamerParser.String_atomContext => Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ", context.exception),
                },
                ScriptRenamerLexer.NE => (context.number_atom(1) ?? (object)context.string_atom(1)) switch
                {
                    ScriptRenamerParser.Number_atomContext => !Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    ScriptRenamerParser.String_atomContext => !Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr NE", context.exception),
                },
                ScriptRenamerLexer.AND => (bool)Visit(context.bool_expr(0)) && (bool)Visit(context.bool_expr(1)),
                ScriptRenamerLexer.OR => (bool)Visit(context.bool_expr(0)) || (bool)Visit(context.bool_expr(1)),
                ScriptRenamerLexer.LPAREN => (bool)Visit(context.bool_expr(0)),
                null => (context.bool_atom() ?? (object)context.collection_expr()) switch
                {
                    ScriptRenamerParser.Bool_atomContext => (bool)Visit(context.bool_atom()),
                    ScriptRenamerParser.Collection_exprContext => ((ICollection)Visit(context.collection_expr())).Count > 0,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in bool_expr NE", context.exception),
                },
                _ => throw new ParseCanceledException("Could not parse bool_expr", context.exception),
            };
        }

        public override object VisitCollection_expr([NotNull] ScriptRenamerParser.Collection_exprContext context)
        {
            return (context.AUDIOCODECS().Symbol ?? context.langs ?? context.IMPORTFOLDERS().Symbol ?? context.title_collection_expr() ?? (object)context.collection_labels()) switch
            {
                Antlr4.Runtime.IToken t => t.Type switch
                {
                    ScriptRenamerLexer.AUDIOCODECS => ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type))
                        .Where(c => c.Contains((string)Visit(context.string_atom()))).ToList(),
                    ScriptRenamerLexer.DUBLANGUAGES or ScriptRenamerLexer.SUBLANGUAGES => ((ICollection<TitleLanguage>)GetCollection(context.langs.Type))
                        .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).Select(t => (object)t).ToList(),
                    ScriptRenamerLexer.IMPORTFOLDERS => ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type))
                        .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals((string)Visit(context.string_atom()))).Select(f => (object)f).ToList(),
                    _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
                },
                ScriptRenamerParser.Title_collection_exprContext => ((ICollection<AnimeTitle>)Visit(context.title_collection_expr())).Select(at => (object)at).ToList(),
                ScriptRenamerParser.Collection_labelsContext => (ICollection)Visit(context.collection_labels()),
                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception),
            };
        }

        public override object VisitTitle_collection_expr([NotNull] ScriptRenamerParser.Title_collection_exprContext context)
        {
            if (context.title_collection_expr() is not null)
            {
                var result = (List<AnimeTitle>)Visit(context.title_collection_expr());
                if (context.title_collection_expr() is not null && context.language_enum() is not null)
                {
                    return result.Where(at => at.Language == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList();
                }
                else if (context.title_collection_expr() is not null && context.titleType_enum() is not null)
                {
                    return result.Where(at => at.Type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList();
                }
            }
            else if (context.titles is not null)
            {
                List<AnimeTitle> result = null;
                if (context.ANIMETITLES() is not null)
                {
                    result = ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type)).ToList();
                }
                else if (context.EPISODETITLES() is not null)
                {
                    result = ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type)).ToList();
                }
                if (context.language_enum() is not null)
                {
                    return result.Where(at => at.Language == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList();
                }
                else if (context.titleType_enum() is not null)
                {
                    return result.Where(at => at.Type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList();
                }
            }
            throw new ParseCanceledException("Could not parse title_collection_expr", context.exception);
        }
        #endregion expressions

        #region labels
        public override object VisitBool_labels([NotNull] ScriptRenamerParser.Bool_labelsContext context)
        {
            return context.label.Type switch
            {
                ScriptRenamerLexer.RESTRICTED => AnimeInfo.Restricted,
                ScriptRenamerLexer.CENSORED => FileInfo.AniDBFileInfo?.Censored ?? false,
                ScriptRenamerLexer.CHAPTERED => FileInfo.MediaInfo?.Chaptered ?? false,
                _ => throw new ParseCanceledException("Could not parse bool_labels", context.exception),
            };
        }

        public override object VisitString_labels([NotNull] ScriptRenamerParser.String_labelsContext context)
        {
            return context.label.Type switch
            {
                ScriptRenamerLexer.ANIMETITLEPREFERRED => AnimeInfo.PreferredTitle,
                ScriptRenamerLexer.ANIMETITLEROMAJI => AnimeTitleLanguage(TitleLanguage.Romaji),
                ScriptRenamerLexer.ANIMETITLEENGLISH => AnimeTitleLanguage(TitleLanguage.English),
                ScriptRenamerLexer.ANIMETITLEJAPANESE => AnimeTitleLanguage(TitleLanguage.Japanese),
                ScriptRenamerLexer.EPISODETITLEROMAJI => EpisodeTitleLanguage(TitleLanguage.Romaji),
                ScriptRenamerLexer.EPISODETITLEENGLISH => EpisodeTitleLanguage(TitleLanguage.English),
                ScriptRenamerLexer.EPISODETITLEJAPANESE => EpisodeTitleLanguage(TitleLanguage.Japanese),
                ScriptRenamerLexer.GROUPSHORT => FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName,
                ScriptRenamerLexer.GROUPLONG => FileInfo.AniDBFileInfo?.ReleaseGroup?.Name,
                ScriptRenamerLexer.CRCLOWER => FileInfo.Hashes.CRC.ToLower(),
                ScriptRenamerLexer.CRCUPPER => FileInfo.Hashes.CRC.ToUpper(),
                ScriptRenamerLexer.SOURCE => FileInfo.AniDBFileInfo?.Source,
                ScriptRenamerLexer.RESOLUTION => FileInfo.MediaInfo?.Video?.StandardizedResolution,
                ScriptRenamerLexer.ANIMETYPE => AnimeInfo.Type.ToString(),
                ScriptRenamerLexer.EPISODETYPE => EpisodeInfo.Type.ToString(),
                ScriptRenamerLexer.EPISODEPREFIX => EpisodeInfo.Type switch
                {
                    EpisodeType.Episode => "",
                    EpisodeType.Special => "S",
                    EpisodeType.Credits => "C",
                    EpisodeType.Trailer => "T",
                    EpisodeType.Parody => "P",
                    EpisodeType.Other => "O",
                    _ => ""
                },
                ScriptRenamerLexer.VIDEOCODECLONG => FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec ?? FileInfo.MediaInfo?.Video?.Codec,
                ScriptRenamerLexer.VIDEOCODECSHORT => FileInfo.MediaInfo?.Video?.SimplifiedCodec,
                ScriptRenamerLexer.DURATION => FileInfo.MediaInfo?.General?.Duration,
                ScriptRenamerLexer.GROUPNAME => GroupInfo?.Name,
                ScriptRenamerLexer.OLDFILENAME => FileInfo.Filename,
                ScriptRenamerLexer.ORIGINALFILENAME => FileInfo.AniDBFileInfo?.OriginalFilename,
                _ => throw new ParseCanceledException("Could not parse string_labels", context.exception),
            };
        }

        public override object VisitCollection_labels([NotNull] ScriptRenamerParser.Collection_labelsContext context)
        {
            return context.label.Type switch
            {
                ScriptRenamerLexer.AUDIOCODECS => ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type)),
                ScriptRenamerLexer.DUBLANGUAGES => ((ICollection<TitleLanguage>)GetCollection(context.DUBLANGUAGES().Symbol.Type)).Select(t => (object)t),
                ScriptRenamerLexer.SUBLANGUAGES => ((ICollection<TitleLanguage>)GetCollection(context.SUBLANGUAGES().Symbol.Type)).Select(t => (object)t),
                ScriptRenamerLexer.ANIMETITLES => ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type)).Select(t => (object)t),
                ScriptRenamerLexer.EPISODETITLES => ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type)).Select(t => (object)t),
                ScriptRenamerLexer.IMPORTFOLDERS => ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type)).Select(t => (object)t),
                _ => throw new ParseCanceledException("Could not parse collection labels", context.exception),
            };
        }

        public override object VisitNumber_labels([NotNull] ScriptRenamerParser.Number_labelsContext context)
        {
            return context.label.Type switch
            {
                ScriptRenamerLexer.EPISODENUMBER => EpisodeInfo.Number,
                ScriptRenamerLexer.FILEVERSION => FileInfo.AniDBFileInfo?.Version,
                ScriptRenamerLexer.WIDTH => FileInfo.MediaInfo?.Video?.Width,
                ScriptRenamerLexer.HEIGHT => FileInfo.MediaInfo?.Video.Height,
                ScriptRenamerLexer.YEAR => AnimeInfo.AirDate?.Year,
                ScriptRenamerLexer.EPISODECOUNT => AnimeInfo.EpisodeCounts.Episodes,
                ScriptRenamerLexer.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth,
                ScriptRenamerLexer.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio.Select(a => a.Channels).Max(),
                _ => throw new ParseCanceledException("Could not parse number_labels", context.exception),
            };
        }
        #endregion labels

        #region atoms
        public override object VisitBool_atom([NotNull] ScriptRenamerParser.Bool_atomContext context)
        {
            if (context.string_atom() is not null)
            {
                return !string.IsNullOrEmpty((string)Visit(context.string_atom()));
            }
            else if (context.bool_labels() is not null)
            {
                return Visit(context.bool_labels());
            }
            else if (context.number_atom() is not null)
            {
                return Convert.ToDouble(Visit(context.number_atom())) <= 0.0;
            }
            else if (context.BOOLEAN() is not null)
            {
                return bool.Parse(context.BOOLEAN().GetText());
            }
            throw new ParseCanceledException("Could not parse bool_atom", context.exception);
        }

        public override object VisitString_atom([NotNull] ScriptRenamerParser.String_atomContext context)
        {
            if (context.number_atom() is not null)
            {
                return Visit(context.number_atom()).ToString();
            }
            else if (context.string_labels() is not null)
            {
                return Visit(context.string_labels());
            }
            else if (context.collection_labels() is not null)
            {
                if (context.collection_labels().AUDIOCODECS() is not null)
                {
                    return ((ICollection<string>)GetCollection(context.collection_labels().AUDIOCODECS().Symbol.Type)).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().DUBLANGUAGES() is not null)
                {
                    return ((ICollection<TitleLanguage>)GetCollection(context.collection_labels().DUBLANGUAGES().Symbol.Type)).Select(t => t.ToString()).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().SUBLANGUAGES() is not null)
                {
                    return ((ICollection<TitleLanguage>)GetCollection(context.collection_labels().SUBLANGUAGES().Symbol.Type)).Select(t => t.ToString()).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().ANIMETITLES() is not null)
                {
                    return ((ICollection<AnimeTitle>)GetCollection(context.collection_labels().ANIMETITLES().Symbol.Type)).Select(a => a.Title).Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().EPISODETITLES() is not null)
                {
                    return ((ICollection<AnimeTitle>)GetCollection(context.collection_labels().EPISODETITLES().Symbol.Type)).Select(a => a.Title).Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().IMPORTFOLDERS() is not null)
                {
                    return ((ICollection<IImportFolder>)GetCollection(context.collection_labels().IMPORTFOLDERS().Symbol.Type)).Select(a => a.Name).DefaultIfEmpty().Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                throw new ParseCanceledException("Could not parse collection labels in string_atom", context.exception);
            }
            else if (context.STRING() is not null)
            {
                return context.STRING().GetText().Trim(new char[] { '\'', '"' });
            }
            else if (context.collection_expr() is not null)
            {
                dynamic result = ((ICollection<object>)Visit(context.collection_expr())).FirstOrDefault();
                if (result is null)
                {
                    return string.Empty;
                }
                return result switch
                {
                    AnimeTitle a => a.Title,
                    TitleLanguage t => t.ToString(),
                    IImportFolder f => f.Name,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in string_atom", context.exception),
                };
            }
            throw new ParseCanceledException("Could not parse string_atom", context.exception);
        }

        public override object VisitNumber_atom([NotNull] ScriptRenamerParser.Number_atomContext context)
        {
            if (context.number_labels() is not null)
            {
                return Visit(context.number_labels());
            }
            else if (context.string_atom() is not null)
            {
                return ((string)Visit(context.string_atom())).Length;
            }
            else if (context.NUMBER() is not null)
            {
                return int.Parse(context.NUMBER().GetText());
            }
            throw new ParseCanceledException("Could not parse number_atom", context.exception);
        }
        #endregion atoms

        #region statements
        public override object VisitIf_stmt([NotNull] ScriptRenamerParser.If_stmtContext context)
        {
            bool result = (bool)Visit(context.bool_expr());
            if (result)
            {
                if (context.non_if_stmt() is not null)
                {
                    _ = Visit(context.non_if_stmt());
                }
                else if (context.true_branch is not null)
                {
                    _ = Visit(context.true_branch);
                }
            }
            else if (context.false_branch is not null)
            {
                _ = Visit(context.false_branch);
            }
            return null;
        }

        public override object VisitSet_stmt([NotNull] ScriptRenamerParser.Set_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == ScriptRenamerLexer.DESTINATION || target == ScriptRenamerLexer.SUBFOLDER) ^ Renaming)
            {
                var setstring = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                switch (target)
                {
                    case ScriptRenamerLexer.FILENAME:
                        Filename = setstring;
                        break;
                    case ScriptRenamerLexer.DESTINATION:
                        Destination = setstring;
                        break;
                    case ScriptRenamerLexer.SUBFOLDER:
                        Subfolder = setstring;
                        break;
                    default:
                        Filename = setstring;
                        break;
                }
            }
            return null;
        }

        public override object VisitAdd_stmt([NotNull] ScriptRenamerParser.Add_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == ScriptRenamerLexer.DESTINATION || target == ScriptRenamerLexer.SUBFOLDER) ^ Renaming)
            {
                var addString = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
                switch (target)
                {
                    case ScriptRenamerLexer.FILENAME:
                        Filename += addString;
                        break;
                    case ScriptRenamerLexer.DESTINATION:
                        Destination += addString;
                        break;
                    case ScriptRenamerLexer.SUBFOLDER:
                        Subfolder += addString;
                        break;
                    default:
                        Filename += addString;
                        break;
                }
            }
            return null;
        }

        public override object VisitReplace_stmt([NotNull] ScriptRenamerParser.Replace_stmtContext context)
        {
            var target = context.target_labels()?.label.Type;
            if ((target == ScriptRenamerLexer.DESTINATION || target == ScriptRenamerLexer.SUBFOLDER) ^ Renaming)
            {
                var oldstr = (string)Visit(context.string_atom(0));
                var newstr = (string)Visit(context.string_atom(1));
                switch (target)
                {
                    case ScriptRenamerLexer.FILENAME:
                        Filename = Filename.Replace(oldstr, newstr);
                        break;
                    case ScriptRenamerLexer.DESTINATION:
                        Destination = Destination.Replace(oldstr, newstr);
                        break;
                    case ScriptRenamerLexer.SUBFOLDER:
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
                ScriptRenamerLexer.AUDIOCODECS => FileInfo.AniDBFileInfo?.MediaInfo?.AudioCodecs.Distinct().ToList()
                                               ?? FileInfo.MediaInfo?.Audio.Select(a => a.SimplifiedCodec).Distinct().ToList()
                                               ?? new List<string>(),
                ScriptRenamerLexer.DUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages.Distinct().ToList()
                                                ?? FileInfo.MediaInfo?.Audio.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).Distinct().ToList()
                                                ?? new List<TitleLanguage>(),
                ScriptRenamerLexer.SUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.SubLanguages.Distinct().ToList()
                                                ?? FileInfo.MediaInfo?.Subs.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).Distinct().ToList()
                                                ?? new List<TitleLanguage>(),
                ScriptRenamerLexer.ANIMETITLES => AnimeInfo.Titles.ToList(),
                ScriptRenamerLexer.EPISODETITLES => EpisodeInfo.Titles.ToList(),
                ScriptRenamerLexer.IMPORTFOLDERS => AvailableFolders,
                _ => throw new KeyNotFoundException("Could not find token type for collection"),
            };
        }
        #endregion utility
    }
}