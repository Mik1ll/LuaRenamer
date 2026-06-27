using System;
using System.Collections.Generic;
using System.Linq;
using NLua;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;

namespace LuaRenamer.LuaEnv.Prototype;

/// <summary>
/// Builds an <see cref="AnimeModel"/> graph from Shoko's <see cref="IAnidbAnime"/>. This is the
/// model-architecture counterpart of <c>LuaContext.AnimeToTable</c>: the same field-by-field mapping,
/// but it produces a plain decoupled model instead of mutating a live LuaTable, and there is no
/// reference-dedup cache (intentionally dropped). Materialization is left to <see cref="LuaSerializer"/>.
/// </summary>
/// <remarks>
/// <paramref name="getname"/> is the shared <c>_getName(self, lang, include_unofficial)</c> Lua
/// function (the production binding for <c>getname</c>), passed in rather than synthesized so the
/// producer stays free of host wiring. It becomes the <see cref="LuaFn{T}.Script"/> case.
/// </remarks>
public static class AnimeModelProducer
{
    public static AnimeModel AnimeToModel(IAnidbAnime anime, LuaFunction getname, bool includeRelations = true)
    {
        ArgumentNullException.ThrowIfNull(anime);
        var series = anime.ShokoSeries.FirstOrDefault();
        return new AnimeModel
        {
            getname = getname,
            airdate = ProducerCommon.DateTimeToModel(anime.AirDate?.ToDateTime()),
            enddate = ProducerCommon.DateTimeToModel(anime.EndDate?.ToDateTime()),
            rating = anime.Rating,
            restricted = anime.Restricted,
            type = anime.Type,
            preferredname = string.IsNullOrWhiteSpace(series?.Title) ? anime.Title : series.Title,
            defaultname = string.IsNullOrWhiteSpace(series?.DefaultTitle.Value) ? anime.DefaultTitle.Value : series.DefaultTitle.Value,
            id = anime.ID,
            titles = anime.Titles.OrderBy(t => t.Value).Select(ProducerCommon.TitleToModel).ToList(),
            studios = anime.Studios.Select(st => st.Name).ToList(),
            episodecounts = Enum.GetValues<EpisodeType>().Distinct().ToDictionary(ep => ep, ep => (long)anime.EpisodeCounts[ep]),
            relations = includeRelations
                ? anime.RelatedSeries.Where(r => r.Related is not null && r.Related.ID != anime.ID).Select(r => RelationToModel(r, getname)).ToList()
                : [],
            tags = anime.Tags.Select(t => t.Name).ToList(),
            customtags = (series?.Tags.Select(t => t.Name) ?? []).ToList(),
            seasons = anime.YearlySeasons.Select(ProducerCommon.SeasonToModel).ToList(),
        };
    }

    private static RelationModel RelationToModel(IRelatedMetadata<ISeries, ISeries> relation, LuaFunction getname) => new()
    {
        // nested anime gets includeRelations: false (mirrors AnimeToTable's ignoreRelations) so the
        // graph terminates without the cache the old code relied on.
        anime = AnimeToModel((relation.Related as IAnidbAnime)!, getname, includeRelations: false),
        type = relation.RelationType,
    };
}
