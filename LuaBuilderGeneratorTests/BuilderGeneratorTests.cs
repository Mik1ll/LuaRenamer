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
        AssertContains("public AnimeTableBuilder rating(double v) { _t[\"rating\"] = v; return this; }");
        AssertContains("public AnimeTableBuilder id(long v) { _t[\"id\"] = v; return this; }");
        AssertContains("public AnimeTableBuilder restricted(bool v) { _t[\"restricted\"] = v; return this; }");
        AssertContains("public AnimeTableBuilder preferredname(string v) { _t[\"preferredname\"] = v; return this; }");
        // Nullable string (PreferredTitle-fed field corrected to string|nil).
        AssertContains("public GroupTableBuilder name(string? v) { _t[\"name\"] = v; return this; }");
    }

    [TestMethod]
    public void EnumMapping()
    {
        // Setter accepts the CLR enum; builder stringifies to match the enum-name Lua tables.
        AssertContains("public AnimeTableBuilder type(global::Shoko.Abstractions.Metadata.Enums.AnimeType v) { _t[\"type\"] = v.ToString(); return this; }");
    }

    [TestMethod]
    public void NestedAndNullableMapping()
    {
        AssertContains("public AnimeTableBuilder airdate(global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.DateTimeTable>? v) { _t[\"airdate\"] = v?.Table; return this; }");
        AssertContains("public AniDbTableBuilder releasegroup(global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.ReleaseGroupTable>? v) { _t[\"releasegroup\"] = v?.Table; return this; }");
        AssertContains("public GroupTableBuilder mainanime(global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.AnimeTable> v) { _t[\"mainanime\"] = v.Table; return this; }");
    }

    [TestMethod]
    public void ArrayMapping()
    {
        AssertContains("public AnimeTableBuilder titles(global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.TitleTable>> v) { _t[\"titles\"] = v.Table; return this; }");
        AssertContains("public AnimeTableBuilder studios(global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<string> v) { _t[\"studios\"] = v.Table; return this; }");
        // Enum array (Language[]) maps to an enum-typed LuaArray.
        AssertContains("public AniDbMediaTableBuilder sublanguages(global::LuaRenamer.LuaEnv.BaseTypes.LuaArray<global::Shoko.Abstractions.Metadata.Enums.TitleLanguage> v) { _t[\"sublanguages\"] = v.Table; return this; }");
    }

    [TestMethod]
    public void MapMapping()
    {
        AssertContains("public AnimeTableBuilder episodecounts(global::LuaRenamer.LuaEnv.BaseTypes.LuaMap<global::Shoko.Abstractions.Metadata.Enums.EpisodeType, long> v) { _t[\"episodecounts\"] = v.Table; return this; }");
        AssertContains("public EnvTableBuilder illegal_chars_map(global::LuaRenamer.LuaEnv.BaseTypes.LuaMap<string, string> v) { _t[\"illegal_chars_map\"] = v.Table; return this; }");
    }

    [TestMethod]
    public void FunctionMapping()
    {
        AssertContains("public AnimeTableBuilder getname(global::NLua.LuaFunction v) { _t[\"getname\"] = v; return this; }");
        AssertContains("public EnvTableBuilder log(global::NLua.LuaFunction v) { _t[\"log\"] = v; return this; }");
    }

    [TestMethod]
    public void ClassidAutoSet()
    {
        // Tables carrying a _classidVal const get _classid auto-set in the builder constructor.
        AssertContains("_t[\"_classid\"] = global::LuaRenamer.LuaEnv.AnimeTable._classidVal;");
        // A table without _classidVal must not reference a _classidVal member.
        Assert.IsFalse(_source.Contains("global::LuaRenamer.LuaEnv.TitleTable._classidVal", StringComparison.Ordinal),
            "TitleTableBuilder should not auto-set _classid.");
    }

    [TestMethod]
    public void OutputFieldsAreSkipped()
    {
        // filename/destination/subfolder are Output = true and have no generated setter.
        Assert.IsFalse(_source.Contains("EnvTableBuilder filename(", StringComparison.Ordinal),
            "filename is an Output field and must not get a setter.");
        Assert.IsFalse(_source.Contains("EnvTableBuilder destination(", StringComparison.Ordinal),
            "destination is an Output field and must not get a setter.");
        Assert.IsFalse(_source.Contains("EnvTableBuilder subfolder(", StringComparison.Ordinal),
            "subfolder is an Output field and must not get a setter.");
    }

    [TestMethod]
    public void EnumGlobalsAndBuildReturns()
    {
        // Enum global setter: Lua name "Language" resolves to CLR TitleLanguage via EnumsTable.
        AssertContains("public EnumsTableBuilder Language(global::LuaRenamer.LuaEnv.BaseTypes.LuaEnumRef<global::Shoko.Abstractions.Metadata.Enums.TitleLanguage> v) { _t[\"Language\"] = v.Table; return this; }");
        // Root builders return the underlying LuaTable; table builders return a typed LuaRef.
        AssertContains("public global::NLua.LuaTable Build() => _t;");
        AssertContains("public global::LuaRenamer.LuaEnv.BaseTypes.LuaRef<global::LuaRenamer.LuaEnv.AnimeTable> Build() => new(_t);");
    }
}
