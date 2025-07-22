using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;
using LuaRenamer.LuaEnv.Attributes;
using LuaRenamer.LuaEnv.BaseTypes;
using Shoko.Plugin.Abstractions.DataModels;

namespace LuaDefsGenerator;

public class Generator
{
    private readonly string _outputPath;

    public Generator(string outputPath) => _outputPath = Path.GetFullPath(outputPath);

    public void GenerateDefinitionFiles()
    {
        GenerateDefsFile();
        GenerateEnumsFile();
        GenerateEnvFile();
    }

    private void GenerateDefsFile()
    {
        var types = typeof(Table).Assembly.DefinedTypes
            .Select(t => new { LuaTypeAttribute = t.GetCustomAttribute<LuaTypeAttribute>(), Type = t })
            .Where(t => t.LuaTypeAttribute is not null)
            .OrderBy(t => t.LuaTypeAttribute!.Type, StringComparer.Ordinal)
            .ToList();
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");


        foreach (var type in types)
        {
            var className = type.LuaTypeAttribute!.Type;
            var functions = new List<(MemberInfo member, LuaTypeAttribute typeAttr)>();
            sb.Append($"---@class (exact) {className}\n");

            // Generate fields
            foreach (var member in type.Type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (member.GetCustomAttribute<LuaTypeAttribute>() is not { } typeAttr)
                    continue;

                if (typeAttr.Type == LuaTypeNames.function)
                    functions.Add((member, typeAttr));
                else
                    sb.Append($"---@field {member.Name} {typeAttr.Type}{(typeAttr.Description is { } desc ? $" # {desc}" : string.Empty)}\n");
            }

            sb.Append($"local {className} = {{}}\n\n");

            foreach (var func in functions)
                GenerateFunctionAnnotations(sb, func.member, func.typeAttr, $"{className}:{func.member.Name}");
        }

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "defs.lua"), sb.ToString());
    }

    private static readonly HashSet<TitleLanguage> AnidbLangs =
    [
        TitleLanguage.Japanese,
        TitleLanguage.Romaji,
        TitleLanguage.English,
        TitleLanguage.Chinese,
        TitleLanguage.ChineseSimplified,
        TitleLanguage.ChineseTraditional,
        TitleLanguage.Pinyin,
        TitleLanguage.Korean,
        TitleLanguage.KoreanTranscription,
        TitleLanguage.Afrikaans,
        TitleLanguage.Albanian,
        TitleLanguage.Arabic,
        TitleLanguage.Bengali,
        TitleLanguage.Bosnian,
        TitleLanguage.Bulgarian,
        TitleLanguage.MyanmarBurmese,
        TitleLanguage.Croatian,
        TitleLanguage.Czech,
        TitleLanguage.Danish,
        TitleLanguage.Dutch,
        TitleLanguage.Esperanto,
        TitleLanguage.Estonian,
        TitleLanguage.Filipino,
        TitleLanguage.Finnish,
        TitleLanguage.French,
        TitleLanguage.Georgian,
        TitleLanguage.German,
        TitleLanguage.Greek,
        TitleLanguage.HaitianCreole,
        TitleLanguage.Hebrew,
        TitleLanguage.Hindi,
        TitleLanguage.Hungarian,
        TitleLanguage.Icelandic,
        TitleLanguage.Indonesian,
        TitleLanguage.Italian,
        TitleLanguage.Javanese,
        TitleLanguage.Latin,
        TitleLanguage.Latvian,
        TitleLanguage.Lithuanian,
        TitleLanguage.Malaysian,
        TitleLanguage.Mongolian,
        TitleLanguage.Nepali,
        TitleLanguage.Norwegian,
        TitleLanguage.Persian,
        TitleLanguage.Polish,
        TitleLanguage.Portuguese,
        TitleLanguage.BrazilianPortuguese,
        TitleLanguage.Romanian,
        TitleLanguage.Russian,
        TitleLanguage.Serbian,
        TitleLanguage.Sinhala,
        TitleLanguage.Slovak,
        TitleLanguage.Slovenian,
        TitleLanguage.Spanish,
        TitleLanguage.Basque,
        TitleLanguage.Catalan,
        TitleLanguage.Galician,
        TitleLanguage.Swedish,
        TitleLanguage.Tamil,
        TitleLanguage.Tatar,
        TitleLanguage.Telugu,
        TitleLanguage.Thai,
        TitleLanguage.ThaiTranscription,
        TitleLanguage.Turkish,
        TitleLanguage.Ukrainian,
        TitleLanguage.Urdu,
        TitleLanguage.Vietnamese,
    ];

    private void GenerateEnumsFile()
    {
        var enumsType = typeof(EnumsTable);
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");
        foreach (var prop in enumsType.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var enumType = prop.PropertyType.GenericTypeArguments[0];

            sb.Append($"---@enum {prop.Name}\n");
            sb.Append($"{prop.Name} = {{\n");

            if (enumType == typeof(TitleLanguage))
            {
                var lkup = Enum.GetValues<TitleLanguage>().ToLookup(t => t switch
                {
                    TitleLanguage.Japanese or TitleLanguage.Romaji or TitleLanguage.English or TitleLanguage.Chinese or TitleLanguage.Pinyin
                        or TitleLanguage.Korean or TitleLanguage.KoreanTranscription => 0,
                    TitleLanguage.Unknown or TitleLanguage.Main or TitleLanguage.None => 3,
                    _ => AnidbLangs.Contains(t) ? 1 : 2,
                }, t => t.ToString());
                sb.Append("\n--#region AniDB Languages\n");
                CreateMappings(lkup[0]);
                sb.Append('\n');
                CreateMappings(lkup[1].Order(StringComparer.Ordinal));
                sb.Append("--#endregion\n");
                sb.Append("\n--#region Other Languages\n");
                CreateMappings(lkup[2].Order(StringComparer.Ordinal));
                sb.Append("--#endregion\n\n");
                CreateMappings(lkup[3]);
            }
            else
            {
                CreateMappings(Enum.GetNames(enumType));
            }

            sb.Append("}\n\n");
        }

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "enums.lua"), sb.ToString());
        return;

        void CreateMappings(IEnumerable<string> enumerable)
        {
            foreach (var name in enumerable)
                sb.Append($"    {name} = \"{name}\",\n");
        }
    }

    private static void GenerateFunctionAnnotations(StringBuilder sb, MemberInfo member, LuaTypeAttribute typeAttr, string functionName)
    {
        if (typeAttr.Description is { } description)
            sb.Append($"---{description}\n");

        var parameters = member.GetCustomAttributes<LuaParameterAttribute>().ToList();
        foreach (var param in parameters)
            sb.Append($"---@param {param.Name} {param.Type} {param.Description}\n");

        if (member.GetCustomAttribute<LuaReturnTypeAttribute>() is { } returnAttr)
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
            if (prop.GetCustomAttribute<LuaTypeAttribute>() is not { } typeAttr)
                continue;

            if (typeAttr.Type == LuaTypeNames.function)
            {
                GenerateFunctionAnnotations(sb, prop, typeAttr, prop.Name);
            }
            else
            {
                if (typeAttr.Description is { } description)
                    sb.Append($"---{description}\n");
                sb.Append($"---@type {typeAttr.Type}\n");
                sb.Append($"{prop.Name} = {typeAttr.DefaultValue}\n\n");
            }
        }

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "env.lua"), sb.ToString());
    }
}
