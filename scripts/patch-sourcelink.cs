#!/usr/bin/env dotnet run
#:package Mono.Cecil@0.11.6
// The repo uses Central Package Management (Directory.Packages.props), which forbids a
// Version on PackageReference; opt this standalone tool out so #:package can pin one.
#:property ManagePackageVersionsCentrally=false
// Copy NuGet assemblies next to the built app. Without this the file-based app's
// runtimeconfig omits additionalProbingPaths, so Mono.Cecil isn't found at runtime.
#:property CopyLocalLockFileAssemblies=true

// Inject SourceLink into a NuGet package's assembly+PDB so the debugger can resolve its
// source from GitHub. For packages whose shipped PDBs lack SourceLink (e.g. NLua) and
// whose build path collides with another package's (NLua & KeraLua were both built at
// "D:\a\1\s", so a local sourceFileMap for one hijacks the other — SourceLink avoids that
// by being per-module). The patched assembly+PDB pair is written to .lib-src/patched/;
// a Debug-only MSBuild target in Directory.Build.props copies it over the restored package
// files in each project's output (global NuGet cache untouched). .NET (Core) does not
// verify strong names at load, so the re-emitted (unsigned) assembly loads fine; run the
// tests to confirm behavior is unchanged.
//
// Usage (from anywhere; no shell required):
//   dotnet run scripts/patch-sourcelink.cs -- <inputDll> <pdbBuildRoot> <githubRawBase>
//
// Example (NLua 1.7.8, tag v1.7.8 = commit e589b89...). In PowerShell:
//   dotnet run scripts/patch-sourcelink.cs -- `
//     "$env:USERPROFILE\.nuget\packages\nlua\1.7.8\lib\net8.0\NLua.dll" `
//     'D:\a\1\s' `
//     https://raw.githubusercontent.com/NLua/NLua/e589b89bbf3be503c4bb9188748194fde255bc3b
//
// Find <pdbBuildRoot> by inspecting the PDB's document paths (the CI build root). The
// mapping appends "/*", so document "<root>\src\X.cs" resolves to "<githubRawBase>/src/X.cs"
// — verify that URL returns 200 first.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: <inputDll> <pdbBuildRoot> <githubRawBase>");
    Console.Error.WriteLine(@"  e.g. NLua.dll  D:\a\1\s  https://raw.githubusercontent.com/NLua/NLua/<commit>");
    return 1;
}

var dll = args[0];
var buildRoot = args[1];   // e.g. D:\a\1\s  (backslashes; passed literally, NOT JSON-escaped)
var urlBase = args[2];     // e.g. https://raw.githubusercontent.com/NLua/NLua/<commit>

// Output is always <repo>/.lib-src/patched, derived from this file's location so the tool
// can be run from any working directory without a wrapper script computing the repo root.
var outDir = Path.Combine(RepoRoot(), ".lib-src", "patched");
Directory.CreateDirectory(outDir);

// Build the SourceLink JSON HERE so escaping is correct. Passing the finished JSON as a
// command-line arg mangles backslashes on Windows (\\ -> \), producing invalid JSON that
// the debugger silently ignores. Escape backslashes (then quotes) so the Windows build
// root serializes as a valid JSON string: "D:\a\1\s\*" -> "D:\\a\\1\\s\\*". (Manual rather
// than JsonSerializer: file-based apps disable reflection-based serialization by default.)
static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
var json = "{\"documents\":{\"" + Esc(buildRoot + @"\*") + "\":\"" + Esc(urlBase + "/*") + "\"}}";
Console.WriteLine("SourceLink: " + json);

var rp = new ReaderParameters
{
    ReadSymbols = true,
    SymbolReaderProvider = new PortablePdbReaderProvider(),
    InMemory = true,
};
using var module = ModuleDefinition.ReadModule(dll, rp);
for (int i = module.CustomDebugInformations.Count - 1; i >= 0; i--)
    if (module.CustomDebugInformations[i] is SourceLinkDebugInformation)
        module.CustomDebugInformations.RemoveAt(i);
module.CustomDebugInformations.Add(new SourceLinkDebugInformation(json));

var outDll = Path.Combine(outDir, Path.GetFileName(dll));
module.Write(outDll, new WriterParameters
{
    WriteSymbols = true,
    SymbolWriterProvider = new PortablePdbWriterProvider(),
});
Console.WriteLine("Wrote " + outDll);
return 0;

// This file lives in <repo>/scripts/, so the repo root is its parent directory.
// CallerFilePath is baked at compile time to this source file's path; GetFullPath
// guards against it being recorded relative (resolved against the run directory).
static string RepoRoot([CallerFilePath] string path = "") =>
    Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(path))!)!.FullName;
