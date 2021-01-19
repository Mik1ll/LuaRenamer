using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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

        public override object VisitIf_stmt([NotNull] ScriptRenamerParser.If_stmtContext context)
        {
            bool result = (bool)Visit(context.bool_expr());
            if (result)
            {
                if (context.non_if_stmt() is not null)
                {
                    Visit(context.non_if_stmt());
                }
                else if (context.true_branch is not null)
                {
                    Visit(context.true_branch);
                }
                else
                {
                    return new ParseCanceledException("Could not parse if_expr");
                }
            }
            else if (context.ELSE() is not null)
            {
                Visit(context.false_branch);
            }
            return null;
        }

        public override object VisitBool_expr([NotNull] ScriptRenamerParser.Bool_exprContext context)
        {
            var op = context.op;
            switch (op?.Type)
            {
                case ScriptRenamerLexer.NOT:
                    return Visit(context.bool_expr(0));
                case ScriptRenamerLexer.IS:
                    if (context.ANIMETYPE() is not null)
                    {
                        return AnimeInfo.Type == (AnimeType)Enum.Parse(typeof(AnimeType), context.animeType_enum().GetText());
                        //switch (AnimeInfo.Type)
                        //{
                        //    case AnimeType.Movie:
                        //        return context.animeType_enum().MOVIE() is not null;
                        //    case AnimeType.OVA:
                        //        return context.animeType_enum().OVA() is not null;
                        //    case AnimeType.TVSeries:
                        //        return context.animeType_enum().TVSERIES() is not null;
                        //    case AnimeType.TVSpecial:
                        //        return context.animeType_enum().WEB() is not null;
                        //    case AnimeType.Other:
                        //        return context.animeType_enum().OTHER() is not null;
                        //    default:
                        //}
                        //return new ParseCanceledException("Could not find AnimeType Enum in bool_expr IS");
                    }
                    return new ParseCanceledException("Could not find matching operands for bool_expr IS");
                case ScriptRenamerLexer.GT:
                    return (double)Visit(context.number_atom(0)) > (double)Visit(context.number_atom(1));
                case ScriptRenamerLexer.GE:
                    return (double)Visit(context.number_atom(0)) >= (double)Visit(context.number_atom(1));
                case ScriptRenamerLexer.LT:
                    return (double)Visit(context.number_atom(0)) < (double)Visit(context.number_atom(1));
                case ScriptRenamerLexer.LE:
                    return (double)Visit(context.number_atom(0)) <= (double)Visit(context.number_atom(1));
                case ScriptRenamerLexer.EQ:
                    if (context.number_atom(0) is not null && context.number_atom(1) is not null)
                    {
                        var expr1 = Visit(context.number_atom(0));
                        var expr2 = Visit(context.number_atom(1));
                        return expr1 == expr2;
                    }
                    else if (context.string_atom(0) is not null && context.string_atom(1) is not null)
                    {
                        var expr1 = Visit(context.string_atom(0));
                        var expr2 = Visit(context.string_atom(1));
                        return expr1.Equals(expr2);
                    }
                    return new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ");
                case ScriptRenamerLexer.NE:
                    if (context.number_atom(0) is not null && context.number_atom(1) is not null)
                    {
                        var expr1 = Visit(context.number_atom(0));
                        var expr2 = Visit(context.number_atom(1));
                        return expr1 != expr2;
                    }
                    else if (context.string_atom(0) is not null && context.string_atom(1) is not null)
                    {
                        var expr1 = Visit(context.string_atom(0));
                        var expr2 = Visit(context.string_atom(1));
                        return !expr1.Equals(expr2);
                    }
                    return new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ");
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
                        return (((ICollection<object>)Visit(context.collection_expr()))?.Count ?? 0) > 0;
                    }
                    break;
            }
            return new ParseCanceledException("Could not parse bool_expr", context.exception);
        }

        public override object VisitCollection_expr([NotNull] ScriptRenamerParser.Collection_exprContext context)
        {
            if (context.AUDIOCODECS() is not null)
            {
                var codecs = FileInfo.AniDBFileInfo?.MediaInfo?.AudioCodecs.Where(c => c.Contains(context.codec_enum().STRING().GetText())).ToList()
                          ?? FileInfo.MediaInfo?.Audio.Select(a => a.Codec).Where(c => c.Contains(context.codec_enum().STRING().GetText())).ToList();
                return codecs;
            }
            else if (context.langs is not null)
            {
                if (context.langs.Type == ScriptRenamerLexer.DUBLANGUAGES)
                {
                    var langs = FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages
                                .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text))
                                .Select(l => l.ToString()).ToList()
                             ?? FileInfo.MediaInfo?.Audio
                                .Select(a => a.LanguageName)
                                .Where(l => l.Contains(context.language_enum().lang.Text)).ToList();
                    return langs;
                }
                else if (context.langs.Type == ScriptRenamerLexer.SUBLANGUAGES)
                {
                    var langs = FileInfo.AniDBFileInfo?.MediaInfo?.AudioLanguages
                                .Where(l => l == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text))
                                .Select(l => l.ToString()).ToList()
                             ?? FileInfo.MediaInfo?.Audio.Select(a => a.LanguageName).Where(l => l == context.language_enum().lang.Text).ToList();
                }
            }
            else if (context.title_collection_expr() is not null)
            {
                return ((ICollection<(TitleLanguage, TitleType)>)Visit(context.title_collection_expr())).Select(t => t.Item1.ToString());
            }
            return new ParseCanceledException("Could not parse collection_expr", context.exception);
        }

        public override object VisitTitle_collection_expr([NotNull] ScriptRenamerParser.Title_collection_exprContext context)
        {
            if (context.title_collection_expr() is not null)
            {
                var result = (List<(TitleLanguage lang, TitleType type)>)Visit(context.title_collection_expr());
                if (context.title_collection_expr() is not null && context.language_enum() is not null)
                {
                    return result.Where(lt => lt.lang == (TitleLanguage)Enum.Parse(typeof(TitleLanguage), context.language_enum().lang.Text));
                }
                else if (context.title_collection_expr() is not null && context.titleType_enum() is not null)
                {
                    return result.Where(lt => lt.type == (TitleType)Enum.Parse(typeof(TitleType), context.titleType_enum().GetText()));
                }
            }
            else if (context.titles is not null)
            {
                List<(TitleLanguage lang, TitleType type)> result = null;
                if (context.ANIMETITLES() is not null)
                {
                    result = AnimeInfo.Titles.Select(t => (t.Language, t.Type)).ToList();
                }
                else if (context.EPISODETITLES() is not null)
                {
                    result = EpisodeInfo.Titles.Select(t => (t.Language, t.Type)).ToList();
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
            return new ParseCanceledException("Could not parse title_collection_expr", context.exception);
        }

        public override object VisitBool_atom([NotNull] ScriptRenamerParser.Bool_atomContext context)
        {
            if (context.bool_labels() is not null)
            {
                return Visit(context.bool_labels());
            }
            else if (context.BOOLEAN() is not null)
            {
                return bool.Parse(context.BOOLEAN().GetText());
            }
            return new ParseCanceledException("Could not parse bool_atom", context.exception);
        }

        public override object VisitBool_labels([NotNull] ScriptRenamerParser.Bool_labelsContext context)
        {
            if (context.string_labels() is not null)
            {
                return !string.IsNullOrEmpty((string)Visit(context.string_labels()));
            }
            else if (context.RESTRICTED() is not null)
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
            return new ParseCanceledException("Could not parse bool_labels", context.exception);
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
            return new ParseCanceledException("Could not parse string_labels", context.exception);
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
            if (context.number_labels() is not null)
            {
                return Visit(context.number_labels()).ToString();
            }
            else if (context.string_labels() is not null)
            {
                return Visit(context.string_labels());
            }
            else if (context.STRING() is not null)
            {
                return context.STRING().GetText().Trim(new char[] { '\'', '"' });
            }
            return new ParseCanceledException("Could not parse string_atom", context.exception);
        }

        public override object VisitNumber_labels([NotNull] ScriptRenamerParser.Number_labelsContext context)
        {
            if (context.EPISODECOUNT() is not null)
            {
                return (double)AnimeInfo.EpisodeCounts.Episodes;
            }
            return new ParseCanceledException("Could not parse number_labels", context.exception);
        }

        public override object VisitNumber_atom([NotNull] ScriptRenamerParser.Number_atomContext context)
        {
            if (context.number_labels() is not null)
            {
                return Visit(context.number_labels());
            }
            else
            {
                return double.Parse(context.NUMBER().GetText());
            }
        }

    }
}