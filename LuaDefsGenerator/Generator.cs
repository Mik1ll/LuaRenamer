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
            .Where(t => t.Namespace == "LuaRenamer.LuaEnv" && t.IsSubclassOf(typeof(Table)))
            .OrderBy(t => t.Name.Replace("Table", ""), StringComparer.Ordinal)
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

    private static void GenerateFunctionAnnotations(StringBuilder sb, MemberInfo member, string functionName)
    {
        var parameters = member.GetCustomAttributes<LuaParameterAttribute>().ToList();
        var returnAttr = member.GetCustomAttribute<LuaReturnTypeAttribute>();

        AppendDescriptionAnnotation(sb, member);

        foreach (var param in parameters)
            sb.Append($"---@param {param.Name} {param.Type} {param.Description}\n");

        if (returnAttr is not null)
        {
            var type = returnAttr.Type;
            if (returnAttr.Nillable)
                type += "|nil";
            sb.Append($"---@return {type}\n");
        }

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

        sb.Length--;

        File.WriteAllText(Path.Combine(_outputPath, "env.lua"), sb.ToString());
    }

    private static void AppendDescriptionAnnotation(StringBuilder sb, MemberInfo member)
    {
        var descAttr = member.GetCustomAttribute<LuaDescriptionAttribute>();
        if (descAttr is not null)
            sb.Append($"---{descAttr.Description}\n");
    }
}
