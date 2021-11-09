using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime.Misc;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using SRP = ScriptRenamerParser;

namespace ScriptRenamer
{
    public class ScriptRenamerVisitor : ScriptRenamerBaseVisitor<object>
    {
        public string Destination;
        public string Filename;
        public string Subfolder;

        public ScriptRenamerVisitor()
        {
        }

        public ScriptRenamerVisitor(RenameEventArgs args)
        {
            Renaming = true;
            AvailableFolders = new List<IImportFolder>();
            Init(args);
        }

        public ScriptRenamerVisitor(MoveEventArgs args)
        {
            Renaming = false;
            AvailableFolders = args.AvailableFolders;
            Init(new RenameEventArgs
            {
                AnimeInfo = args.AnimeInfo,
                EpisodeInfo = args.EpisodeInfo,
                Script = args.Script,
                FileInfo = args.FileInfo,
                GroupInfo = args.GroupInfo,
                Cancel = args.Cancel
            });
        }

        public bool Renaming { get; set; } = true;
        public bool FindLastLocation { get; set; }
        public bool RemoveReservedChars { get; set; }

        public List<IImportFolder> AvailableFolders { get; init; } = new();
        public IVideoFile FileInfo { get; set; }
        public IAnime AnimeInfo { get; set; }
        public IGroup GroupInfo { get; set; }
        public IEpisode EpisodeInfo { get; set; }
        public IRenameScript Script { get; set; }
        public List<IEpisode> Episodes { get; set; }

        private int LastEpisodeNumber { get; set; }

        private void Init(RenameEventArgs args)
        {
            AnimeInfo = args.AnimeInfo.FirstOrDefault();
            EpisodeInfo = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID)
                .OrderBy(e => e.Type == EpisodeType.Other ? (EpisodeType)int.MinValue : e.Type)
                .ThenBy(e => e.Number)
                .FirstOrDefault();
            var seq = EpisodeInfo?.Number - 1 ?? 0;
            LastEpisodeNumber = args.EpisodeInfo.Where(e => e.AnimeID == AnimeInfo?.AnimeID && e.Type == EpisodeInfo?.Type)
                .OrderBy(e => e.Number).TakeWhile(e => e.Number == (seq += 1)).LastOrDefault()?.Number ?? -1;
            FileInfo = args.FileInfo;
            GroupInfo = args.GroupInfo?.FirstOrDefault();
            Script = args.Script;
            Episodes = new List<IEpisode>(args.EpisodeInfo);
        }

        #region expressions

        public override object VisitBool_expr([NotNull] SRP.Bool_exprContext context)
        {
            return context.op?.Type switch
            {
                SRP.NOT => !(bool)Visit(context.bool_expr(0)),
                SRP.IS => context.is_left.Type switch
                {
                    SRP.ANIMETYPE => AnimeInfo.Type == ParseEnum<AnimeType>(context.ANIMETYPE_ENUM().GetText()),
                    SRP.EPISODETYPE => EpisodeInfo.Type == ParseEnum<EpisodeType>(context.EPISODETYPE_ENUM().GetText()),
                    _ => throw new ParseCanceledException("Could not find matching operands for bool_expr IS", context.exception)
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
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr EQ", context.exception)
                },
                SRP.NE => (context.bool_expr(0) ?? context.number_atom(0) ?? (object)context.string_atom(0)) switch
                {
                    SRP.Number_atomContext => !Equals(Visit(context.number_atom(0)), Visit(context.number_atom(1))),
                    SRP.String_atomContext => !Equals(Visit(context.string_atom(0)), Visit(context.string_atom(1))),
                    SRP.Bool_exprContext => (bool)Visit(context.bool_expr(0)) != (bool)Visit(context.bool_expr(1)),
                    _ => throw new ParseCanceledException("Could not parse strings or numbers in bool_expr NE", context.exception)
                },
                SRP.AND => (bool)Visit(context.bool_expr(0)) && (bool)Visit(context.bool_expr(1)),
                SRP.OR => (bool)Visit(context.bool_expr(0)) || (bool)Visit(context.bool_expr(1)),
                SRP.LPAREN => (bool)Visit(context.bool_expr(0)),
                SRP.CONTAINS => ((string)Visit(context.string_atom(0)))?.Contains((string)Visit(context.string_atom(1)) ?? string.Empty) ?? false,
                null => (context.bool_atom() ?? (object)context.collection_expr()) switch
                {
                    SRP.Bool_atomContext => (bool)Visit(context.bool_atom()),
                    SRP.Collection_exprContext => ((IList)Visit(context.collection_expr())).Count > 0,
                    _ => throw new ParseCanceledException("Could not parse collection_expr in bool_expr NE", context.exception)
                },
                _ => throw new ParseCanceledException("Could not parse bool_expr", context.exception)
            };
        }

        public override IList VisitCollection_expr([NotNull] SRP.Collection_exprContext context)
        {
            var rhsString = context.string_atom() is not null ? (string)Visit(context.string_atom()) : string.Empty;
            if (context.collection_labels() is not null)
                return (IList)Visit(context.collection_labels());
            var collection = (IList)Visit(context.collection_expr());

            int type = 0;
            SRP.Collection_exprContext exprContext = context;
            while (type == 0)
            {
                if (exprContext.collection_labels() is null)
                    exprContext = exprContext.collection_expr();
                else
                    type = exprContext.collection_labels().label.Type;
            }

            return (context.FIRST()?.Symbol.Type ?? type) switch
            {
                SRP.AUDIOCODECS when rhsString is not null => ((List<string>)collection).Where(c => c.Contains(rhsString)).ToList(),
                SRP.SUBLANGUAGES or SRP.DUBLANGUAGES when context.LANGUAGE_ENUM() is not null => ((List<TitleLanguage>)collection)
                    .Where(l => l == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText())).ToList(),
                SRP.IMPORTFOLDERS when rhsString is not null => ((List<IImportFolder>)collection).Where(f =>
                    string.Equals(f.Name, rhsString, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ScriptRenamer.NormPath(f.Location), ScriptRenamer.NormPath(rhsString), StringComparison.OrdinalIgnoreCase)).ToList(),
                SRP.ANIMETITLES or SRP.EPISODETITLES when (context.TITLETYPE_ENUM() ?? context.LANGUAGE_ENUM()) is not null => ((List<AnimeTitle>)collection)
                    .Where(at => context.TITLETYPE_ENUM() is null || at.Type == ParseEnum<TitleType>(context.TITLETYPE_ENUM().GetText()))
                    .Where(at => context.LANGUAGE_ENUM() is null || at.Language == ParseEnum<TitleLanguage>(context.LANGUAGE_ENUM().GetText())).ToList(),
                SRP.FIRST => collection.Take(1).ToList(),
                _ => throw new ParseCanceledException("Could not parse collection_expr", context.exception)
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
                SRP.MULTILINKED => Episodes.Count(e => e.AnimeID == AnimeInfo.AnimeID) > 1,
                _ => throw new ParseCanceledException("Could not parse bool_labels", context.exception)
            };
        }

        public override object VisitString_labels([NotNull] SRP.String_labelsContext context)
        {
            int pad = context.number_atom() is null ? 0 : (int)Visit(context.number_atom());
            return context.label.Type switch
            {
                SRP.ANIMETITLEPREFERRED => AnimeInfo.PreferredTitle,
                SRP.ANIMETITLEROMAJI => AnimeTitleLanguage(TitleLanguage.Romaji),
                SRP.ANIMETITLEENGLISH => AnimeTitleLanguage(TitleLanguage.English),
                SRP.ANIMETITLEJAPANESE => AnimeTitleLanguage(TitleLanguage.Japanese),
                SRP.EPISODETITLEROMAJI => EpisodeTitleLanguage(TitleLanguage.Romaji),
                SRP.EPISODETITLEENGLISH => EpisodeTitleLanguage(TitleLanguage.English),
                SRP.EPISODETITLEJAPANESE => EpisodeTitleLanguage(TitleLanguage.Japanese),
                SRP.GROUPSHORT =>
                    FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName == "raw"
                        ? null
                        : FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName,
                SRP.GROUPLONG =>
                    FileInfo.AniDBFileInfo?.ReleaseGroup?.Name == "raw/unknown"
                        ? null
                        : FileInfo.AniDBFileInfo?.ReleaseGroup?.Name,
                SRP.CRCLOWER => FileInfo.Hashes.CRC.ToLower(),
                SRP.CRCUPPER => FileInfo.Hashes.CRC.ToUpper(),
                SRP.SOURCE => FileInfo.AniDBFileInfo?.Source.Contains("unknown", StringComparison.OrdinalIgnoreCase) ?? true
                    ? null
                    : FileInfo.AniDBFileInfo.Source,
                SRP.RESOLUTION => FileInfo.MediaInfo?.Video?.StandardizedResolution,
                SRP.ANIMETYPE => AnimeInfo.Type.ToString(),
                SRP.EPISODETYPE => EpisodeInfo.Type.ToString(),
                SRP.EPISODEPREFIX => GetPrefix(EpisodeInfo.Type),
                SRP.VIDEOCODECLONG => FileInfo.MediaInfo?.Video?.CodecID,
                SRP.VIDEOCODECSHORT => FileInfo.MediaInfo?.Video?.SimplifiedCodec,
                SRP.VIDEOCODECANIDB => FileInfo.AniDBFileInfo?.MediaInfo?.VideoCodec,
                SRP.DURATION => FileInfo.MediaInfo?.General?.Duration.ToString(CultureInfo.InvariantCulture),
                SRP.GROUPNAME => GroupInfo?.Name,
                SRP.OLDFILENAME => System.IO.Path.GetFileNameWithoutExtension(FileInfo.Filename),
                SRP.ORIGINALFILENAME => System.IO.Path.GetFileNameWithoutExtension(FileInfo.AniDBFileInfo?.OriginalFilename),
                SRP.OLDIMPORTFOLDER => OldDestination()?.Location,
                SRP.FILENAME => Filename,
                SRP.SUBFOLDER => Subfolder,
                SRP.DESTINATION => Destination,
                SRP.EPISODENUMBERS => Episodes.Where(e => e.AnimeID == AnimeInfo?.AnimeID)
                    .OrderBy(e => e.Number)
                    .GroupBy(e => e.Type)
                    .OrderBy(g => g.Key)
                    .Aggregate("", (s, g) =>
                        s + " " + g.Aggregate(
                            (InRun: false, Seq: -1, Str: ""),
                            (tup, ep) => ep.Number == tup.Seq + 1
                                ? (true, ep.Number, tup.Str)
                                : tup.InRun
                                    ? (false, ep.Number, $"{tup.Str}-{tup.Seq.PadZeroes(pad)} {GetPrefix(g.Key)}{ep.Number.PadZeroes(pad)}")
                                    : (false, ep.Number, $"{tup.Str} {GetPrefix(g.Key)}{ep.Number.PadZeroes(pad)}"),
                            tup => tup.InRun ? $"{tup.Str}-{tup.Seq.PadZeroes(pad)}" : tup.Str
                        ).Trim()
                    ).Trim(),
                _ => throw new ParseCanceledException("Could not parse string_labels", context.exception)
            };

            static string GetPrefix(EpisodeType episodeInfoType)
            {
                return episodeInfoType switch
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
                _ => throw new ParseCanceledException("Could not parse collection labels", context.exception)
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
                SRP.EPISODECOUNT => EpisodeInfo.Type switch
                {
                    EpisodeType.Episode => AnimeInfo.EpisodeCounts.Episodes,
                    EpisodeType.Special => AnimeInfo.EpisodeCounts.Specials,
                    EpisodeType.Credits => AnimeInfo.EpisodeCounts.Credits,
                    EpisodeType.Trailer => AnimeInfo.EpisodeCounts.Trailers,
                    EpisodeType.Parody => AnimeInfo.EpisodeCounts.Parodies,
                    EpisodeType.Other => AnimeInfo.EpisodeCounts.Others,
                    _ => throw new ParseCanceledException("Could not parse EpisodeCount", context.exception)
                },
                SRP.BITDEPTH => FileInfo.MediaInfo?.Video?.BitDepth ?? 0,
                SRP.AUDIOCHANNELS => FileInfo.MediaInfo?.Audio?.Select(a => a.Channels).Max() ?? 0,
                SRP.SERIESINGROUP => GroupInfo?.Series.Count ?? 1,
                SRP.LASTEPISODENUMBER => LastEpisodeNumber,
                SRP.MAXEPISODECOUNT => new[]
                {
                    AnimeInfo.EpisodeCounts.Episodes,
                    AnimeInfo.EpisodeCounts.Specials,
                    AnimeInfo.EpisodeCounts.Credits,
                    AnimeInfo.EpisodeCounts.Trailers,
                    AnimeInfo.EpisodeCounts.Parodies,
                    AnimeInfo.EpisodeCounts.Others
                }.Max(),
                _ => throw new ParseCanceledException("Could not parse number_labels", context.exception)
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
                _ => throw new ParseCanceledException("Could not parse bool_atom", context.exception)
            };
        }

        public override object VisitString_atom([NotNull] SRP.String_atomContext context)
        {
            return context.op?.Type switch
            {
                SRP.PAD => ((int)Visit(context.number_atom(0))).PadZeroes((int)Visit(context.number_atom(1))),
                SRP.STRING => context.STRING().GetText()[1..^1],
                SRP.PLUS => (string)Visit(context.string_atom(0)) + (string)Visit(context.string_atom(1)),
                SRP.REPLACE => (string)Visit(context.string_atom(1)) is var temp && !string.IsNullOrEmpty(temp)
                    ? ((string)Visit(context.string_atom(0)))?.Replace(temp, (string)Visit(context.string_atom(2)))
                    : (string)Visit(context.string_atom(0)),
                SRP.RXREPLACE => Regex.Replace((string)Visit(context.string_atom(0)), (string)Visit(context.string_atom(1)),
                    (string)Visit(context.string_atom(2))),
                SRP.SUBSTRING => (string)Visit(context.string_atom(0)) is var str
                                 && (int)Visit(context.number_atom(0)) is var num1
                                 && context.number_atom(1) is not null
                    ? ((int)Visit(context.number_atom(1)) is var num2 && num1 < str?.Length
                        ? str.Substring(num1, num1 + num2 <= str.Length ? num2 : str.Length - num1)
                        : string.Empty)
                    : (num1 < str?.Length
                        ? str.Substring(num1)
                        : string.Empty),
                SRP.TRUNCATE => (string)Visit(context.string_atom(0)) is var temp
                    ? temp?.Substring(0, Math.Min(temp.Length, (int)Visit(context.number_atom(0))))
                    : null,
                SRP.TRIM => ((string)Visit(context.string_atom(0))).Trim(),
                null => (context.number_atom(0) ?? context.string_labels() ?? context.date_atom() ?? (object)context.collection_expr()) switch
                {
                    SRP.Number_atomContext => Visit(context.number_atom(0)).ToString(),
                    SRP.String_labelsContext => Visit(context.string_labels()),
                    SRP.Collection_exprContext => ((IList)Visit(context.collection_expr())).CollectionString(),
                    SRP.Date_atomContext => Visit(context.date_atom()),
                    _ => throw new ParseCanceledException("Could not parse string_atom with null op label", context.exception)
                },
                SRP.RXMATCH => Regex.Match((string)Visit(context.string_atom(0)), (string)Visit(context.string_atom(1))).Value,
                SRP.UPPER => ((string)Visit(context.string_atom(0))).ToUpper(),
                SRP.LOWER => ((string)Visit(context.string_atom(0))).ToLower(),
                SRP.CAPITALIZE => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((string)Visit(context.string_atom(0))),
                _ => throw new ParseCanceledException("Could not parse string_atom", context.exception)
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
                _ => throw new ParseCanceledException("Could not parse number_atom", context.exception)
            };
        }

        public override object VisitDate_atom([NotNull] SRP.Date_atomContext context)
        {
            var date = context.type.Type switch
            {
                SRP.ANIMERELEASEDATE => AnimeInfo.AirDate,
                SRP.EPISODERELEASEDATE => EpisodeInfo.AirDate,
                SRP.FILERELEASEDATE => FileInfo.AniDBFileInfo?.ReleaseDate,
                _ => throw new ParseCanceledException("Could not parse date_atom", context.exception)
            };
            if (context.DOT() is not null)
                return context.field.Type switch
                {
                    SRP.YEAR => date?.Year.ToString(),
                    SRP.MONTH => date?.Month.ToString(),
                    SRP.DAY => date?.Day.ToString(),
                    _ => throw new ParseCanceledException("Could not parse date_atom DOT", context.exception)
                };
            return date?.ToString("yyyy.MM.dd");
        }

        #endregion atoms

        #region statements

        public override object VisitIf_stmt([NotNull] SRP.If_stmtContext context)
        {
            var result = (bool)Visit(context.bool_expr());
            if (result)
                _ = Visit(context.true_branch);
            else if (context.false_branch is not null)
                _ = Visit(context.false_branch);
            return null;
        }

        public override object VisitCtrl([NotNull] SRP.CtrlContext context)
        {
            return (context.if_stmt() ?? (object)context.block()) switch
            {
                SRP.If_stmtContext => Visit(context.if_stmt()),
                SRP.BlockContext => Visit(context.block()),
                _ => throw new ParseCanceledException("Could not parse VisitCtrl")
            };
        }

        public override object VisitStmt([NotNull] SRP.StmtContext context)
        {
            var ctx = context.cancel?.Type ?? context.FINDLASTLOCATION()?.Symbol.Type ??
                context.REMOVERESERVEDCHARS()?.Symbol.Type ?? (object)context.target_labels()?.label.Type;
            return ctx switch
            {
                SRP.CANCEL => throw new ParseCanceledException(
                    $"Line {context.cancel.Line} Column {context.cancel.Column} Cancelled: {AggregateString()}"),
                SRP.SKIPRENAME => Renaming ? throw new SkipException() : null,
                SRP.SKIPMOVE => Renaming ? null : throw new SkipException(),
                SRP.FINDLASTLOCATION => FindLastLocation = true,
                SRP.DESTINATION when !Renaming => DoAction(ref Destination),
                SRP.SUBFOLDER when !Renaming => DoAction(ref Subfolder),
                SRP.REMOVERESERVEDCHARS => RemoveReservedChars = true,
                not (SRP.DESTINATION or SRP.SUBFOLDER) when Renaming && context.op is not null => DoAction(ref Filename),
                _ when context.op is not null => null,
                _ => throw new ParseCanceledException("Could not parse VisitStmt")
            };

            object DoAction(ref string target)
            {
                return context.op.Type switch
                {
                    SRP.SET => target = AggregateString(),
                    SRP.ADD => target += AggregateString(),
                    SRP.REPLACE => target = target.Replace((string)Visit(context.string_atom(0)), (string)Visit(context.string_atom(1))),
                    _ => throw new ParseCanceledException("Could not parse action statement", context.exception)
                };
            }

            string AggregateString()
            {
                return context.string_atom()?.Select(a =>
                    a.STRING() is null || context.target_labels()?.label.Type != SRP.SUBFOLDER
                        ? (string)Visit(a)
                        : ((string)Visit(a)).Replace('/', (char)0x1F).Replace('\\', (char)0x1F)
                ).Aggregate((s1, s2) => s1 + s2);
            }
        }

        #endregion statements

        #region utility

        private string AnimeTitleLanguage(TitleLanguage language)
        {
            var titles = AnimeInfo.Titles.Where(t => t.Language == language).ToList<AnimeTitle>();
            return (titles.FirstOrDefault(t => t.Type == TitleType.Main)
                    ?? titles.FirstOrDefault(t => t.Type == TitleType.Official)
                    ?? titles.FirstOrDefault()
                )?.Title;
        }

        private string EpisodeTitleLanguage(TitleLanguage language)
        {
            var titles = EpisodeInfo.Titles.Where(t => t.Language == language).ToList<AnimeTitle>();
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
                                    ?? FileInfo.MediaInfo?.Audio?.Select(a => ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown ? ParseEnum<TitleLanguage>(a.Title, false) : l).Distinct().ToList()
                                    ?? new List<TitleLanguage>(),
                SRP.SUBLANGUAGES => FileInfo.AniDBFileInfo?.MediaInfo?.SubLanguages?.Distinct().ToList()
                                    ?? FileInfo.MediaInfo?.Subs?.Select(a => ParseEnum<TitleLanguage>(a.LanguageName, false) is var l && l is TitleLanguage.Unknown ? ParseEnum<TitleLanguage>(a.Title, false) : l).Distinct().ToList()
                                    ?? new List<TitleLanguage>(),
                SRP.ANIMETITLES => AnimeInfo.Titles.ToList(),
                SRP.EPISODETITLES => EpisodeInfo.Titles.ToList(),
                SRP.IMPORTFOLDERS => AvailableFolders.Where(i => i.DropFolderType.HasFlag(DropFolderType.Destination)).ToList(),
                _ => throw new KeyNotFoundException("Could not find token type for collection")
            };
        }

        private static T ParseEnum<T>(string text, bool throwException = true)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), text, true);
            }
            catch
            {
                if (throwException)
                    throw;
                return default;
            }
        }

        private IImportFolder OldDestination()
        {
            return AvailableFolders.OrderByDescending(f => f.Location.Length)
                .FirstOrDefault(f =>
                    $"{ScriptRenamer.NormPath(FileInfo.FilePath)}/".StartsWith($"{ScriptRenamer.NormPath(f.Location)}/", StringComparison.OrdinalIgnoreCase));
        }

        #endregion utility
    }
}
