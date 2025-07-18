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
            .Where(t => t.Namespace == "LuaRenamer.LuaEnv" && t.IsSubclassOf(typeof(Table))).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("---@meta\n");

        foreach (var type in types)
        {
            var className = type.Name.Replace("Table", "");
            sb.AppendLine($"---@class (exact) {className}");

            // Generate fields
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var typeAttr = prop.GetCustomAttribute<LuaTypeAttribute>();
                if (typeAttr is null)
                    continue;

                var fieldName = prop.Name;
                if (typeAttr.Type == "function")
                {
                    GenerateFunctionAnnotations(sb, prop, $"{className}:{fieldName}");
                    continue;
                }

                var luaType = typeAttr.Type;
                if (typeAttr.Nillable)
                    luaType += "|nil";

                var descAttr = prop.GetCustomAttribute<LuaDescriptionAttribute>();

                sb.AppendLine($"---@field {fieldName} {luaType}{(descAttr != null ? $" # {descAttr.Description}" : "")}");
            }

            sb.AppendLine($"local {className} = {{}}\n");
        }

        File.WriteAllText(Path.Combine(_outputPath, "defs.lua"), sb.ToString());
    }

    private void GenerateEnumsFile()
    {
        var sb = new StringBuilder();
        sb.AppendLine("---@meta\n");

        // Add your enum types here if any
        // This would come from a separate configuration or scanning of enum types

        File.WriteAllText(Path.Combine(_outputPath, "enums.lua"), sb.ToString());
    }

    private static void GenerateFunctionAnnotations(StringBuilder sb, PropertyInfo prop, string functionName)
    {
        var parameters = prop.GetCustomAttributes<LuaParameterAttribute>().ToList();
        var returnAttr = prop.GetCustomAttribute<LuaReturnTypeAttribute>();

        AppendDescriptionAnnotation(sb, prop);

        foreach (var param in parameters)
            sb.AppendLine($"---@param {param.Name} {param.Type} {param.Description}");

        if (returnAttr is not null)
            sb.AppendLine($"---@return {returnAttr.Type}");

        sb.AppendLine($"function {functionName}({string.Join(", ", parameters.Select(p => p.Name))}) end\n");
    }

    private void GenerateEnvFile()
    {
        var envType = typeof(EnvTable);
        var sb = new StringBuilder();
        sb.AppendLine("---@meta\n");

        // Generate global variable definitions
        foreach (var prop in envType.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var typeAttr = prop.GetCustomAttribute<LuaTypeAttribute>();
            if (typeAttr is null)
                continue;

            var fieldName = prop.Name;
            if (typeAttr.Type == "function")
            {
                GenerateFunctionAnnotations(sb, prop, fieldName);
                continue;
            }

            AppendDescriptionAnnotation(sb, prop);

            var type = typeAttr.Type;
            if (typeAttr.Nillable)
                type += "|nil";

            sb.AppendLine($"---@type {type}");
            sb.AppendLine($"{fieldName} = {typeAttr.DefaultValue}\n");
        }

        File.WriteAllText(Path.Combine(_outputPath, "env.lua"), sb.ToString());
    }

    private static void AppendDescriptionAnnotation(StringBuilder sb, PropertyInfo prop)
    {
        var descAttr = prop.GetCustomAttribute<LuaDescriptionAttribute>();
        if (descAttr is not null)
            sb.AppendLine($"---{descAttr.Description}");
    }
}
