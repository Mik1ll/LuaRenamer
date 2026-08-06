using LuaRenamer.DefsGenerator;

// The only place in this project that touches the filesystem; the generators just return strings.
//
// Usage:
//   DefsGenerator <luaOutputDir>     write defs.lua / enums.lua / env.lua into the directory
//   DefsGenerator --names <file.cs>  write the C# *Names navigation DSL to the file
const string usage = "usage: DefsGenerator <luaOutputDir> | DefsGenerator --names <file.cs>";

switch (args)
{
    case ["--names", var namesFile]:
        Write(Path.GetFullPath(namesFile), new ModelNamesGenerator().GenerateNames());
        break;

    case [var luaDir] when !luaDir.StartsWith('-'):
        var generator = new ModelDefsGenerator();
        var dir = Path.GetFullPath(luaDir);
        Write(Path.Combine(dir, "defs.lua"), generator.GenerateDefs());
        Write(Path.Combine(dir, "enums.lua"), generator.GenerateEnums());
        Write(Path.Combine(dir, "env.lua"), generator.GenerateEnv());
        break;

    default:
        Console.Error.WriteLine(usage);
        return 1;
}

return 0;

static void Write(string path, string contents)
{
    if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
        Directory.CreateDirectory(dir);
    File.WriteAllText(path, contents);
}
