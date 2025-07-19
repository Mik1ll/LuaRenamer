using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;

namespace LuaRenamer.LuaEnv;

public class LuaDocsGenerator
{
    private readonly string _outputPath;

    public LuaDocsGenerator(string outputPath) => _outputPath = outputPath;

    public void GenerateDefinitionFiles()
    {
        GenerateDefsFile();
        GenerateEnumsFile();
        GenerateEnvFile();
    }

    private void GenerateDefsFile()
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.Namespace == "LuaRenamer.LuaEnv" && t.IsSubclassOf(typeof(Table)))
            .OrderBy(t => t.Name)
            .ToList();
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");


        foreach (var type in types)
        {
            var className = type.Name.Replace("Table", "");
            var functions = new List<MemberInfo>();
            sb.Append($"---@class (exact) {className}\n");

            // Generate fields
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                var typeAttr = member.GetCustomAttribute<LuaTypeAttribute>();
                if (typeAttr is null)
                    continue;

                if (typeAttr.Type == "function")
                {
                    functions.Add(member);
                    continue;
                }

                var luaType = typeAttr.Type;
                if (typeAttr.Nillable)
                    luaType += "|nil";

                var descAttr = member.GetCustomAttribute<LuaDescriptionAttribute>();

                sb.Append($"---@field {member.Name} {luaType}{(descAttr is not null ? $" # {descAttr.Description}" : "")}\n");
            }

            sb.Append($"local {className} = {{}}\n\n");

            foreach (var member in functions)
                GenerateFunctionAnnotations(sb, member, $"{className}:{member.Name}");
        }

        File.WriteAllText(Path.Combine(_outputPath, "defs.lua"), sb.ToString());
    }

    private void GenerateEnumsFile()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        // Add your enum types here if any
        // This would come from a separate configuration or scanning of enum types

        File.WriteAllText(Path.Combine(_outputPath, "enums.lua"), sb.ToString());
    }

    private static void GenerateFunctionAnnotations(StringBuilder sb, MemberInfo member, string functionName)
    {
        var parameters = member.GetCustomAttributes<LuaParameterAttribute>().ToList();
        var returnAttr = member.GetCustomAttribute<LuaReturnTypeAttribute>();

        AppendDescriptionAnnotation(sb, member);

        foreach (var param in parameters)
            sb.Append($"---@param {param.Name} {param.Type} {param.Description}\n");

        if (returnAttr is not null)
            sb.Append($"---@return {returnAttr.Type}\n");

        sb.Append($"function {functionName}({string.Join(", ", parameters.Select(p => p.Name))}) end\n\n");
    }

    private void GenerateEnvFile()
    {
        var envType = typeof(EnvTable);
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        // Generate global variable definitions
        foreach (var prop in envType.GetMembers(BindingFlags.Public | BindingFlags.Static))
        {
            var typeAttr = prop.GetCustomAttribute<LuaTypeAttribute>();
            if (typeAttr is null)
                continue;

            if (typeAttr.Type == "function")
            {
                GenerateFunctionAnnotations(sb, prop, prop.Name);
                continue;
            }

            AppendDescriptionAnnotation(sb, prop);

            var type = typeAttr.Type;
            if (typeAttr.Nillable)
                type += "|nil";

            sb.Append($"---@type {type}\n");
            sb.Append($"{prop.Name} = {typeAttr.DefaultValue}\n\n");
        }

        File.WriteAllText(Path.Combine(_outputPath, "env.lua"), sb.ToString());
    }

    private static void AppendDescriptionAnnotation(StringBuilder sb, MemberInfo member)
    {
        var descAttr = member.GetCustomAttribute<LuaDescriptionAttribute>();
        if (descAttr is not null)
            sb.Append($"---{descAttr.Description}\n");
    }
}
