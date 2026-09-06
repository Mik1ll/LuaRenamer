using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Models;
using LuaRenamer.LuaEnv.Names;
using LuaRenamer.Tests.Fakes;
using Microsoft.Extensions.Logging.Testing;
using NLua;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Xunit;

namespace LuaRenamer.Tests.Env;

/// <summary>
/// The enum tables a script sees are identity name maps, and they carry exactly the members the declared
/// enumeration has — no more, no fewer. Checked against both the CLR enumeration and the generated
/// <c>enums.lua</c>, so the two views cannot drift apart.
/// </summary>
public sealed class EnumTableTests : IDisposable
{
    private static readonly EnvNames Names = new();

    private readonly LuaSandbox _sandbox = new(LuaScripts.LuaLinq, LuaScripts.Utils);

    public EnumTableTests() =>
        new ModelTranslator(_sandbox).Translate(
            ModelProducers.EnvToModel(RelocationGraph.Default().Context(""), new FakeLogger<LuaRenamer>()), _sandbox.Env);

    public void Dispose() => _sandbox.Dispose();

    public static TheoryData<string, Type> ExposedEnums() => new()
    {
        { nameof(EnvModel.ImportFolderType), typeof(DropFolderType) },
        { nameof(EnvModel.AnimeType), typeof(AnimeType) },
        { nameof(EnvModel.EpisodeType), typeof(EpisodeType) },
        { nameof(EnvModel.TitleType), typeof(TitleType) },
        { nameof(EnvModel.Language), typeof(TitleLanguage) },
        { nameof(EnvModel.RelationType), typeof(RelationType) },
        { nameof(EnvModel.SeasonName), typeof(YearlySeason) },
    };

    [Theory]
    [MemberData(nameof(ExposedEnums))]
    public void EachExposedEnumIsTheIdentityMapOfItsDeclaredMembers(string luaName, Type enumType)
    {
        LuaTable table = _sandbox.GetValue(luaName).Should().BeOfType<LuaTable>(luaName).Subject;

        Entries(table).Should().OnlyContain(e => Equals(e.Key, e.Value), $"{luaName} is an identity map");
        Entries(table).Select(e => (string)e.Key).Should().BeEquivalentTo(LuaEnumTable.Names(enumType),
            $"{luaName} must expose exactly the members {enumType.Name} declares");
    }

    [Theory]
    [MemberData(nameof(ExposedEnums))]
    public void EachExposedEnumMatchesTheGeneratedDefinition(string luaName, Type enumType)
    {
        _ = enumType;
        using var defs = new Lua();
        defs.DoFile(Path.Combine(LuaScripts.LuaPath, "enums.lua"));

        // enums.lua really does define globals in a plain interpreter, so the NLua indexer works there; the
        // sandbox side has to go through GetValue, which resolves against Env.
        Entries((LuaTable)defs[luaName]).Should().BeEquivalentTo(Entries((LuaTable)_sandbox.GetValue(luaName)!));
    }

    [Fact]
    public void TheNamesDslAgreesWithTheSchemaPaths()
    {
        Names.anime.relations[1].type.ToString().Should()
            .Be($"{nameof(EnvModel.anime)}.{nameof(AnimeModel.relations)}[1].{nameof(RelationModel.type)}");
        Names.Language[TitleLanguage.English].Should().Be($"{nameof(EnvModel.Language)}.{nameof(TitleLanguage.English)}");
    }

    private static List<KeyValuePair<object, object>> Entries(LuaTable table) =>
        [.. table.Keys.Cast<object>().Select(k => new KeyValuePair<object, object>(k, table[k]))];
}
