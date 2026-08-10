using System.Reflection;
using System.Text;
using LuaRenamer.LuaEnv;

namespace LuaRenamer.DefsGenerator;

/// <summary>
/// Produces the three lua-language-server definition documents from the <see cref="ILuaModel"/> records:
/// <c>defs.lua</c> (a class per model), <c>enums.lua</c> (the exposed enum tables) and <c>env.lua</c> (the
/// globals a user script sees).
/// </summary>
/// <remarks>
/// This class decides what goes in each document and in what order. It delegates the three concerns it does
/// not own: <see cref="SchemaReflection"/> classifies properties, <see cref="LuaTypeRenderer"/> turns a type
/// or signature into LuaCATS text, and <see cref="TitleLanguageSections"/> holds the one enum with a bespoke
/// layout. It performs no I/O — <c>Program</c> writes the returned strings.
/// </remarks>
public sealed class ModelDefsGenerator
{
    private readonly LuaTypeRenderer _renderer = new();

    // ---- defs.lua ------------------------------------------------------------------------------

    public string GenerateDefs()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");
        sb.Append(GenerateClassSection());
        sb.Length--; // mirror Generator: drop the trailing newline of the final block
        return sb.ToString();
    }

    public string GenerateClassSection()
    {
        // EnvModel drives env.lua/enums.lua instead of the class section. Ordered by the stripped Lua class
        // name — deliberately not ModelNamesGenerator's CLR-type-name order.
        var types = SchemaReflection.ModelTypes()
            .Where(t => t != typeof(EnvModel))
            .OrderBy(t => SchemaReflection.StripModel(t.Name), StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();

        foreach (var type in types)
        {
            var className = SchemaReflection.StripModel(type.Name);
            var functions = new List<(PropertyInfo prop, LuaFieldAttribute fieldAttr, string sep)>();
            sb.Append($"---@class (exact) {className}\n");

            foreach (var (prop, fieldAttr) in SchemaReflection.LuaFields(type))
            {
                // Callable (implemented in C# or Lua) -> deferred to the function section; ':' if Method else '.'.
                if (SchemaReflection.ContractOf(prop.PropertyType) is not null)
                    functions.Add((prop, fieldAttr, fieldAttr.Method ? ":" : "."));
                else
                    sb.Append($"---@field {prop.Name} {_renderer.TypeOf(prop)}{(fieldAttr.Description is { } d ? $" # {d}" : "")}\n");
            }

            sb.Append($"local {className} = {{}}\n\n");

            foreach (var (prop, fieldAttr, sep) in functions)
                sb.Append(_renderer.FunctionAnnotations(prop, fieldAttr, $"{className}{sep}{prop.Name}"));
        }

        return sb.ToString();
    }

    // ---- enums.lua -----------------------------------------------------------------------------

    public string GenerateEnums()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        foreach (var prop in SchemaReflection.EnumTableProps())
        {
            var enumType = prop.PropertyType.GetGenericArguments()[0];

            sb.Append($"---@enum {prop.Name}\n");
            sb.Append($"{prop.Name} = {{\n");
            // Most enums are a flat list; the one with a bespoke sectioned layout renders itself. Names come
            // from LuaEnumTable, the same source the translator builds the runtime table from.
            sb.Append(TitleLanguageSections.Render(enumType, RenderMappings)
                      ?? RenderMappings(LuaEnumTable.Names(enumType)));
            sb.Append("}\n\n");
        }

        sb.Length--;
        return sb.ToString();
    }

    private static string RenderMappings(IEnumerable<string> names) =>
        string.Concat(names.Select(name => $"    {name} = \"{name}\",\n"));

    // ---- env.lua -------------------------------------------------------------------------------

    public string GenerateEnv()
    {
        var sb = new StringBuilder();
        sb.Append("---@meta\n\n");

        foreach (var (prop, fieldAttr) in SchemaReflection.LuaFields(typeof(EnvModel)).Where(x => !SchemaReflection.IsEnumTable(x.Prop)))
        {
            if (SchemaReflection.ContractOf(prop.PropertyType) is not null)
            {
                sb.Append(_renderer.FunctionAnnotations(prop, fieldAttr, prop.Name));
            }
            else
            {
                if (fieldAttr.Description is { } desc)
                    sb.Append($"---{desc}\n");
                sb.Append($"---@type {_renderer.TypeOf(prop)}\n");
                sb.Append($"{prop.Name} = {fieldAttr.DefaultValue}\n\n");
            }
        }

        sb.Length--;
        return sb.ToString();
    }
}
