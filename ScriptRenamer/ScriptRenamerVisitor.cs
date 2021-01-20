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

        public List<IImportFolder> AvailableFolders { get; set; }
        public IVideoFile FileInfo { get; set; }
        public IAnime AnimeInfo { get; set; }
        public IGroup GroupInfo { get; set; }
        public IEpisode EpisodeInfo { get; set; }
        public IRenameScript Script { get; set; }

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
                else
                {
                    throw new ParseCanceledException("Could not parse if_expr");
                }
            }
            else if (context.ELSE() is not null)
            {
                _ = Visit(context.false_branch);
            }
            return null;
        }

        public override object VisitBool_expr([NotNull] ScriptRenamerParser.Bool_exprContext context)
        {
            var op = context.op;
            switch (op?.Type)
            {
                case ScriptRenamerLexer.NOT:
                    return (bool)Visit(context.bool_expr(0));
                case ScriptRenamerLexer.IS:
                    if (context.ANIMETYPE() is not null)
                    {
                        return AnimeInfo.Type == (AnimeType)Enum.Parse(typeof(AnimeType), context.animeType_enum().GetText());
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
                       .Where(c => c.Contains(context.codec_enum().STRING().GetText())).ToList();
            }
            else if (context.langs is not null)
            {
                return ((ICollection<TitleLanguage>)GetCollection(context.langs.Type))
                       .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList();
            }
            else if (context.IMPORTFOLDERS() is not null)
            {
                return ((ICollection<IImportFolder>)GetCollection(context.langs.Type))
                       .Where(f => f.DropFolderType != DropFolderType.Source && f.Name.Equals(context.STRING().GetText())).ToList();
            }
            else if (context.title_collection_expr() is not null)
            {
                return (ICollection<(TitleLanguage, TitleType)>)Visit(context.title_collection_expr());
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
                var result = (List<(TitleLanguage lang, TitleType type)>)Visit(context.title_collection_expr());
                if (context.title_collection_expr() is not null && context.language_enum() is not null)
                {
                    return result.Where(lt => lt.lang == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList();
                }
                else if (context.title_collection_expr() is not null && context.titleType_enum() is not null)
                {
                    return result.Where(lt => lt.type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList();
                }
            }
            else if (context.titles is not null)
            {
                List<(TitleLanguage lang, TitleType type)> result = null;
                if (context.ANIMETITLES() is not null)
                {
                    result = ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type)).Select(t => (t.Language, t.Type)).ToList();
                }
                else if (context.EPISODETITLES() is not null)
                {
                    result = ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type)).Select(t => (t.Language, t.Type)).ToList();
                }
                if (context.language_enum() is not null)
                {
                    return result.Where(lt => lt.lang == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text)).ToList();
                }
                else if (context.titleType_enum() is not null)
                {
                    return result.Where(lt => lt.type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText())).ToList();
                }
            }
            throw new ParseCanceledException("Could not parse title_collection_expr", context.exception);
        }

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

        private string AnimeTitleLanguage(TitleLanguage language)
        {
            var title = AnimeInfo.Titles.FirstOrDefault(t => t.Type == TitleType.Main && t.Language == language);
            if (title is null)
            {
                if ((title = AnimeInfo.Titles.FirstOrDefault(t => t.Type == TitleType.Official && t.Language == language)) is null)
                {
                    title = AnimeInfo.Titles.FirstOrDefault(t => t.Language == language);
                }
            }
            return title?.Title;
        }

        private string EpisodeTitleLanguage(TitleLanguage language)
        {
            var title = EpisodeInfo.Titles.FirstOrDefault(t => t.Type == TitleType.Main && t.Language == language);
            if (title is null)
            {
                if ((title = EpisodeInfo.Titles.FirstOrDefault(t => t.Type == TitleType.Official && t.Language == language)) is null)
                {
                    title = EpisodeInfo.Titles.FirstOrDefault(t => t.Language == language);
                }
            }
            return title?.Title;
        }

        public override object VisitString_atom([NotNull] ScriptRenamerParser.String_atomContext context)
        {
            if (context.number_atom() is not null)
            {
                string result = Visit(context.number_atom()).ToString();
                if (context.number_atom()?.number_labels()?.EPISODENUMBER() is not null)
                {
                    var prefix = EpisodeInfo.Type switch
                    {
                        EpisodeType.Episode => "",
                        EpisodeType.Special => "S",
                        EpisodeType.Credits => "C",
                        EpisodeType.Trailer => "T",
                        EpisodeType.Parody => "P",
                        EpisodeType.Other => "O",
                        _ => ""
                    };
                    result = prefix + result;
                }
                return result;
            }
            else if (context.string_labels() is not null)
            {
                return (string)Visit(context.string_labels());
            }
            else if (context.collection_labels() is not null)
            {
                if (context.collection_labels().AUDIOCODECS() is not null)
                {
                    return ((ICollection<string>)GetCollection(context.collection_labels().AUDIOCODECS().Symbol.Type)).Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().DUBLANGUAGES() is not null)
                {
                    return ((ICollection<TitleLanguage>)GetCollection(context.collection_labels().DUBLANGUAGES().Symbol.Type)).Cast<string>().Aggregate((s1, s2) => $"{s1}, {s2}");
                }
                else if (context.collection_labels().SUBLANGUAGES() is not null)
                {
                    return ((ICollection<TitleLanguage>)GetCollection(context.collection_labels().SUBLANGUAGES().Symbol.Type)).Cast<string>().Aggregate((s1, s2) => $"{s1}, {s2}");
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
                    return ((ICollection<IImportFolder>)GetCollection(context.collection_labels().IMPORTFOLDERS().Symbol.Type)).Select(a => a.Name).Aggregate((s1, s2) => $"{s1}, {s2}");
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
                return ((dynamic)result.GetType()) switch
                {
                    AnimeTitle a => a.Title,
                    TitleLanguage t => t.ToString(),
                    IImportFolder f => f.Name,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in string_atom", context.exception),
                };
            }
            throw new ParseCanceledException("Could not parse string_atom", context.exception);
        }

        public override object VisitCollection_labels([NotNull] ScriptRenamerParser.Collection_labelsContext context)
        {
            if (context.AUDIOCODECS() is not null)
            {
                return ((ICollection<string>)GetCollection(context.AUDIOCODECS().Symbol.Type));
            }
            else if (context.DUBLANGUAGES() is not null)
            {
                return ((ICollection<TitleLanguage>)GetCollection(context.DUBLANGUAGES().Symbol.Type));
            }
            else if (context.SUBLANGUAGES() is not null)
            {
                return ((ICollection<TitleLanguage>)GetCollection(context.SUBLANGUAGES().Symbol.Type));
            }
            else if (context.ANIMETITLES() is not null)
            {
                return ((ICollection<AnimeTitle>)GetCollection(context.ANIMETITLES().Symbol.Type));
            }
            else if (context.EPISODETITLES() is not null)
            {
                return ((ICollection<AnimeTitle>)GetCollection(context.EPISODETITLES().Symbol.Type));
            }
            else if (context.IMPORTFOLDERS() is not null)
            {
                return ((ICollection<IImportFolder>)GetCollection(context.IMPORTFOLDERS().Symbol.Type));
            }
            throw new ParseCanceledException("Could not parse collection labels", context.exception);
        }

        private IEnumerable GetCollection(int tokenType)
        {
            return tokenType switch
            {
                ScriptRenamerLexer.AUDIOCODECS => FileInfo.AniDBFileInfo?.MediaInfo?.AudioCodecs.ToList()
                                               ?? FileInfo.MediaInfo?.Audio.Select(a => a.Codec).ToList()
                                               ?? new List<string>(),
                ScriptRenamerLexer.DUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages.ToList()
                                                ?? FileInfo.MediaInfo?.Audio.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).ToList()
                                                ?? new List<TitleLanguage>(),
                ScriptRenamerLexer.SUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.SubLanguages.ToList()
                                                ?? FileInfo.MediaInfo?.Subs.Select(a => (TitleLanguage)Enum.Parse(typeof(TitleLanguage), a.LanguageName)).ToList()
                                                ?? new List<TitleLanguage>(),
                ScriptRenamerLexer.ANIMETITLES => AnimeInfo.Titles.ToList(),
                ScriptRenamerLexer.EPISODETITLES => EpisodeInfo.Titles.ToList(),
                ScriptRenamerLexer.IMPORTFOLDERS => AvailableFolders,
                _ => throw new KeyNotFoundException("Could not find token type for collection"),
            };
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

        public override object VisitSet_stmt([NotNull] ScriptRenamerParser.Set_stmtContext context)
        {
            var setstring = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
            switch (context.target().tar.Type)
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
                    throw new ParseCanceledException("Could not parse set_stmt", context.exception);
            }
            return null;
        }

        public override object VisitAdd_stmt([NotNull] ScriptRenamerParser.Add_stmtContext context)
        {
            var addString = context.string_atom().Select(a => (string)Visit(a)).Aggregate((s1, s2) => s1 + s2);
            switch (context.target().tar.Type)
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
                    throw new ParseCanceledException("Could not parse add_stmt", context.exception);
            }
            return null;
        }

        public override object VisitReplace_stmt([NotNull] ScriptRenamerParser.Replace_stmtContext context)
        {
            var oldstr = (string)Visit(context.STRING(0));
            var newstr = (string)Visit(context.STRING(1));
            switch (context.target().tar.Type)
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
                    throw new ParseCanceledException("Could not parse replace_stmt", context.exception);
            }
            return null;
        }
    }
}