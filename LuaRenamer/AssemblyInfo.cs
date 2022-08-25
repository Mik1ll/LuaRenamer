using System.Reflection;

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyTitle(nameof(LuaRenamer.LuaRenamer))]
[assembly: AssemblyProduct(nameof(LuaRenamer.LuaRenamer))]
[assembly: AssemblyVersion("0.3.0")]
[assembly: AssemblyFileVersion("0.3.0")]
