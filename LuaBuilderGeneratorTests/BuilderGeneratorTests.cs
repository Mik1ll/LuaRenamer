using System;
using System.IO;
using System.Linq;
using LuaBuilderGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaBuilderGeneratorTests;

/// <summary>
/// Drives <see cref="BuilderGenerator"/> over a compilation that references the real LuaRenamer
/// schema (via metadata) and asserts the emitted builders. Locks in the <c>[LuaType]</c>-to-C#
/// type-mapping rules and special cases so future schema/generator changes are caught.
/// </summary>
[TestClass]
public class BuilderGeneratorTests
{
    private static string _source = null!;
    private static Compilation _output = null!;
    private static System.Collections.Immutable.ImmutableArray<Diagnostic> _generatorDiagnostics;

    [ClassInitialize]
    public static void RunGenerator(TestContext _)
    {
        // Reference every assembly the test host has loaded (framework + LuaRenamer + NLua + Shoko),
        // so the generator can resolve the real LuaRenamer.LuaEnv schema from metadata and the
        // generated builders compile against the real types.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratorTestAssembly",
            syntaxTrees: [],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // RunGeneratorsAndUpdateCompilation returns a new (immutable) driver holding the run results.
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new BuilderGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _output, out _generatorDiagnostics);

        _source = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("LuaBuilders.g.cs", StringComparison.Ordinal))
            .ToString();
    }

    private static void AssertContains(string expected) =>
        Assert.IsTrue(_source.Contains(expected, StringComparison.Ordinal),
            $"Generated source did not contain:\n{expected}");

    [TestMethod]
    public void GeneratedCodeCompiles()
    {
        Assert.IsFalse(_generatorDiagnostics.Any(),
            "Generator produced diagnostics:\n" + string.Join("\n", _generatorDiagnostics));

        var errors = _output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.IsFalse(errors.Any(),
            "Generated builders failed to compile:\n" + string.Join("\n", errors));
    }

    [TestMethod]
    public void EmitsBuilderPerSchemaTable()
    {
        // A representative table builder, the two root builders, and an inline-only nested builder.
        AssertContains("internal sealed class AnimeTableBuilder");
        AssertContains("internal sealed class EnvTableBuilder");
        AssertContains("internal sealed class EnumsTableBuilder");
        AssertContains("internal sealed class AudioTableBuilder");
    }

    [TestMethod]
    public void ScalarMapping()
    {
        // Each field is a `required` init member that writes straight into the LuaTable.
        AssertContains("public required double rating { init => _t[\"rating\"] = value; }");
        AssertContains("public required long id { init => _t[\"id\"] = value; }");
        AssertContains("public required bool restricted { init => _t[\"restricted\"] = value; }");
        AssertContains("public required string preferredname { init => _t[\"preferredname\"] = value; }");
        // Nullable string (PreferredTitle-fed field corrected to string|nil).
        AssertContains("public required string? name { init => _t[\"name\"] = value; }");
    }

    [TestMethod]
    public void EnumMapping()
    {
        // Member accepts the CLR enum; the setter stringifies to match the enum-name Lua tables.
        AssertContains("public required global::Shoko.Abstractions.Metadata.Enums.AnimeType type { init => _t[\"type\"] = value.ToString(); }");
    }

    [TestMethod]
    public void NestedAndNullableMapping()
    {
        // Nullable ref unwraps with value?.Table, non-null with value.Table.
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.DateTimeTable>? airdate { init => _t[\"airdate\"] = value?.Table; }");
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.ReleaseGroupTable>? releasegroup { init => _t[\"releasegroup\"] = value?.Table; }");
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.AnimeTable> mainanime { init => _t[\"mainanime\"] = value.Table; }");
    }

    [TestMethod]
    public void ArrayMapping()
    {
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.TitleTable>> titles { init => _t[\"titles\"] = value.Table; }");
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<string> studios { init => _t[\"studios\"] = value.Table; }");
        // Enum array (Language[]) maps to an enum-typed LuaArray.
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<global::Shoko.Abstractions.Metadata.Enums.TitleLanguage> sublanguages { init => _t[\"sublanguages\"] = value.Table; }");
    }

    [TestMethod]
    public void MapMapping()
    {
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaMap<global::Shoko.Abstractions.Metadata.Enums.EpisodeType, long> episodecounts { init => _t[\"episodecounts\"] = value.Table; }");
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaMap<string, string> illegal_chars_map { init => _t[\"illegal_chars_map\"] = value.Table; }");
    }

    [TestMethod]
    public void FunctionMapping()
    {
        AssertContains("public required global::NLua.LuaFunction getname { init => _t[\"getname\"] = value; }");
        AssertContains("public required global::NLua.LuaFunction log { init => _t[\"log\"] = value; }");
    }

    [TestMethod]
    public void ClassidAutoSet()
    {
        // Tables carrying a _classidVal const get _classid auto-set in the constructor.
        AssertContains("_t[\"_classid\"] = global::LuaRenamer.LuaEnv.AnimeTable._classidVal;");
        // A table without _classidVal must not reference a _classidVal member.
        Assert.IsFalse(_source.Contains("global::LuaRenamer.LuaEnv.TitleTable._classidVal", StringComparison.Ordinal),
            "TitleTableBuilder should not auto-set _classid.");
    }

    [TestMethod]
    public void OutputFieldsAreSkipped()
    {
        // filename/destination/subfolder are Output = true and get no generated member at all.
        Assert.IsFalse(_source.Contains("filename", StringComparison.Ordinal),
            "filename is an Output field and must not get a member.");
        Assert.IsFalse(_source.Contains("destination", StringComparison.Ordinal),
            "destination is an Output field and must not get a member.");
        Assert.IsFalse(_source.Contains("subfolder", StringComparison.Ordinal),
            "subfolder is an Output field and must not get a member.");
    }

    [TestMethod]
    public void EnumGlobalsAndHandleConversions()
    {
        // Enum global member: Lua name "Language" resolves to CLR TitleLanguage via EnumsTable.
        AssertContains("public required global::LuaRenamer.LuaEnv.BaseTypes.LuaEnumRef<global::Shoko.Abstractions.Metadata.Enums.TitleLanguage> Language { init => _t[\"Language\"] = value.Table; }");
        // Table builders implicitly convert to their typed handle (replacing Build()).
        AssertContains("public static implicit operator global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.AnimeTable>(AnimeTableBuilder b) => new(b._t);");
        // Root builders mutate the env table in place: no handle, no conversion.
        Assert.IsFalse(_source.Contains("LuaRef<global::LuaRenamer.LuaEnv.EnvTable>", StringComparison.Ordinal),
            "EnvTableBuilder is a root builder and must not expose a LuaRef handle.");
        Assert.IsFalse(_source.Contains("LuaRef<global::LuaRenamer.LuaEnv.EnumsTable>", StringComparison.Ordinal),
            "EnumsTableBuilder is a root builder and must not expose a LuaRef handle.");
    }
}
