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
            var op = context.op;
            switch (op?.Type)
            {
                case ScriptRenamerLexer.NOT:
                    return !(bool)Visit(context.bool_expr(0));
                case ScriptRenamerLexer.IS:
                    if (context.ANIMETYPE() is not null)
                    {
                        return AnimeInfo.Type == (AnimeType)Enum.Parse(typeof(AnimeType), context.animeType_enum().GetText());
                    }
                    else if (context.EPISODETYPE() is not null)
                    {
                        return EpisodeInfo.Type == (EpisodeType)Enum.Parse(typeof(EpisodeType), context.episodeType_enum().GetText());
                    }
                    throw new ParseCanceledException("Could not find matching operands for bool_expr IS", context.exception);
                case ScriptRenamerLexer.GT:
                    return Convert.ToDouble(Visit(context.number_atom(0))) > Convert.ToDouble(Visit(context.number_atom(1)));
                case ScriptRenamerLexer.GE:
                    return Convert.ToDouble(Visit(context.number_atom(0))) >= Convert.ToDouble(Visit(context.number_atom(1)));
                case ScriptRenamerLexer.LT:
                    return Convert.ToDouble(Visit(context.number_atom(0))) < Convert.ToDouble(Visit(context.number_atom(1)));
                case ScriptRenamerLexer.LE:
                    return Convert.ToDouble(Visit(context.number_atom(0))) <= Convert.ToDouble(Visit(context.number_atom(1)));
                case ScriptRenamerLexer.EQ:
                    if (context.number_atom(0) is not null && context.number_atom(1) is not null)
                    {
                        var expr1 = Convert.ToDouble(Visit(context.number_atom(0)));
                        var expr2 = Convert.ToDouble(Visit(context.number_atom(1)));
                        return expr1 == expr2;
                    }
                    else if (context.string_atom(0) is not null && context.string_atom(1) is not null)
                    {
                        var expr1 = (string)Visit(context.string_atom(0));
                        var expr2 = (string)Visit(context.string_atom(1));
                        return expr1.Equals(expr2);
                    }
                    throw new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ", context.exception);
                case ScriptRenamerLexer.NE:
                    if (context.number_atom(0) is not null && context.number_atom(1) is not null)
                    {
                        var expr1 = Convert.ToDouble(Visit(context.number_atom(0)));
                        var expr2 = Convert.ToDouble(Visit(context.number_atom(1)));
                        return expr1 != expr2;
                    }
                    else if (context.string_atom(0) is not null && context.string_atom(1) is not null)
                    {
                        var expr1 = (string)Visit(context.string_atom(0));
                        var expr2 = (string)Visit(context.string_atom(1));
                        return !expr1.Equals(expr2);
                    }
                    throw new ParseCanceledException("Could not parse strings or numbers in bool_expr NE", context.exception);
                case ScriptRenamerLexer.AND:
                    return (bool)Visit(context.bool_expr(0)) && (bool)Visit(context.bool_expr(1));
                case ScriptRenamerLexer.OR:
                    return (bool)Visit(context.bool_expr(0)) || (bool)Visit(context.bool_expr(1));
                default:
                    if (context.LPAREN() is not null && context.RPAREN() is not null)
                    {
                        return (bool)Visit(context.bool_expr(0));
                    }
                    else if (context.bool_atom() is not null)
                    {
                        return (bool)Visit(context.bool_atom());
                    }
                    else if (context.collection_expr() is not null)
                    {
                        return (((ICollection)Visit(context.collection_expr()))?.Count ?? 0) > 0;
                    }
                    break;
            }
            throw new ParseCanceledException("Could not parse bool_expr", context.exception);
        }

        public override object VisitCollection_expr([NotNull] ScriptRenamerParser.Collection_exprContext context)
        {
            if (context.AUDIOCODECS() is not null)
            {
                return ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type))
                       .Where(c => c.Contains((string)Visit(context.string_atom()))).ToList();
            }
            else if (context.langs is not null)
            {
                return ((ICollection<TitleLanguage>)GetCollection(context.langs.Type))
                       .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).Select(t => (object)t).ToList();
            }
            else if (context.IMPORTFOLDERS() is not null)
            {
                return ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type))
                       .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals((string)Visit(context.string_atom()))).Select(f => (object)f).ToList();
            }
            else if (context.title_collection_expr() is not null)
            {
                return ((ICollection<AnimeTitle>)Visit(context.title_collection_expr())).Select(at => (object)at).ToList();
            }
            else if (context.collection_labels() is not null)
            {
                return (ICollection)Visit(context.collection_labels());
            }
            throw new ParseCanceledException("Could not parse collection_expr", context.exception);
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
            if (context.RESTRICTED() is not null)
            {
                return AnimeInfo.Restricted;
            }
            else if (context.CENSORED() is not null)
            {
                return FileInfo.AniDBFileInfo?.Censored ?? false;
            }
            else if (context.CHAPTERED() is not null)
            {
                return FileInfo.MediaInfo?.Chaptered ?? false;
            }
            throw new ParseCanceledException("Could not parse bool_labels", context.exception);
        }

        public override object VisitString_labels([NotNull] ScriptRenamerParser.String_labelsContext context)
        {
            if (context.ANIMETITLEPREFERRED() is not null)
            {
                return AnimeInfo.PreferredTitle;
            }
            if (context.ANIMETITLEROMAJI() is not null)
            {
                return AnimeTitleLanguage(TitleLanguage.Romaji);
            }
            else if (context.ANIMETITLEENGLISH() is not null)
            {
                return AnimeTitleLanguage(TitleLanguage.English);
            }
            else if (context.ANIMETITLEJAPANESE() is not null)
            {
                return AnimeTitleLanguage(TitleLanguage.Japanese);
            }
            else if (context.EPISODETITLEROMAJI() is not null)
            {
                return EpisodeTitleLanguage(TitleLanguage.Romaji);
            }
            else if (context.EPISODETITLEENGLISH() is not null)
            {
                return EpisodeTitleLanguage(TitleLanguage.English);
            }
            else if (context.EPISODETITLEJAPANESE() is not null)
            {
                return EpisodeTitleLanguage(TitleLanguage.Japanese);
            }
            else if (context.GROUPSHORT() is not null)
            {
                return FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName;
            }
            else if (context.GROUPLONG() is not null)
            {
                return FileInfo.AniDBFileInfo?.ReleaseGroup?.Name;
            }
            else if (context.CRCLOWER() is not null)
            {
                return FileInfo.Hashes.CRC.ToLower();
            }
            else if (context.CRCUPPER() is not null)
            {
                return FileInfo.Hashes.CRC.ToUpper();
            }
            else if (context.SOURCE() is not null)
            {
                return FileInfo.AniDBFileInfo?.Source;
            }
            else if (context.RESOLUTION() is not null)
            {
                return FileInfo.MediaInfo?.Video?.StandardizedResolution;
            }
            else if (context.ANIMETYPE() is not null)
            {
                return AnimeInfo.Type.ToString();
            }
            else if (context.EPISODETYPE() is not null)
            {
                return EpisodeInfo.Type.ToString();
            }
            else if (context.EPISODEPREFIX() is not null)
            {
                return EpisodeInfo.Type switch
                {
                    EpisodeType.Episode => "",
                    EpisodeType.Special => "S",
                    EpisodeType.Credits => "C",
                    EpisodeType.Trailer => "T",
                    EpisodeType.Parody => "P",
                    EpisodeType.Other => "O",
                    _ => ""
                };
            }
            else if (context.VIDEOCODECLONG() is not null)
            {
                return FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec ?? FileInfo.MediaInfo?.Video?.Codec;
            }
            else if (context.VIDEOCODECSHORT() is not null)
            {
                return FileInfo.MediaInfo?.Video?.SimplifiedCodec;
            }
            else if (context.DURATION() is not null)
            {
                return FileInfo.MediaInfo?.General?.Duration;
            }
            else if (context.GROUPNAME() is not null)
            {
                return GroupInfo?.Name;
            }
            else if (context.OLDFILENAME() is not null)
            {
                return FileInfo.Filename;
            }
            else if (context.ORIGINALFILENAME() is not null)
            {
                return FileInfo.AniDBFileInfo?.OriginalFilename;
            }
            throw new ParseCanceledException("Could not parse string_labels", context.exception);
        }

        public override object VisitCollection_labels([NotNull] ScriptRenamerParser.Collection_labelsContext context)
        {
            switch (context.label)
            {
                case ScriptRenamerLexer.AUDIOCODECS:
                    return ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type));
                case ScriptRenamerLexer.DUBLANGUAGES:
                    return ((ICollection<TitleLanguage>)GetCollection(context.DUBLANGUAGES().Symbol.Type)).Select(t => (object)t);
                case ScriptRenamerLexer.SUBLANGUAGES:
                    return ((ICollection<TitleLanguage>)GetCollection(context.SUBLANGUAGES().Symbol.Type)).Select(t => (object)t);
                case ScriptRenamerLexer.ANIMETITLES:
                    return ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type)).Select(t => (object)t);
                case ScriptRenamerLexer.EPISODETITLES:
                    return ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type)).Select(t => (object)t);
                case ScriptRenamerLexer.IMPORTFOLDERS:
                    return ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type)).Select(t => (object)t);
            }
            throw new ParseCanceledException("Could not parse collection labels", context.exception);
        }

        public override object VisitNumber_labels([NotNull] ScriptRenamerParser.Number_labelsContext context)
        {
            if (context.EPISODENUMBER() is not null)
            {
                return EpisodeInfo.Number;
            }
            else if (context.FILEVERSION() is not null)
            {
                return FileInfo.AniDBFileInfo?.Version;
            }
            else if (context.WIDTH() is not null)
            {
                return FileInfo.MediaInfo?.Video?.Width;
            }
            else if (context.HEIGHT() is not null)
            {
                return FileInfo.MediaInfo?.Video.Height;
            }
            else if (context.YEAR() is not null)
            {
                return AnimeInfo.AirDate?.Year;
            }
            else if (context.EPISODECOUNT() is not null)
            {
                return AnimeInfo.EpisodeCounts.Episodes;
            }
            else if (context.BITDEPTH() is not null)
            {
                return FileInfo.MediaInfo?.Video?.BitDepth;
            }
            else if (context.AUDIOCHANNELS() is not null)
            {
                return FileInfo.MediaInfo?.Audio.Select(a => a.Channels).Max();
            }
            throw new ParseCanceledException("Could not parse number_labels", context.exception);
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
                return double.Parse(context.NUMBER().GetText());
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