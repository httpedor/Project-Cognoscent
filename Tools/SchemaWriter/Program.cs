using System.Reflection;
using System.Text.Json;
using Rpg;
using Rpg.Scripting;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SchemaWriter <expr-schema-path> <tags-schema-path>");
    Console.Error.WriteLine("  Writes the auto-generated expression and tag JSON schemas.");
    return 1;
}

var exprPath = args[0];
var tagsPath = args[1];

// ── Expression schema (unchanged) ──────────────────────────────────────────
ExprJsonSchema.WriteToFile(exprPath);
Console.WriteLine($"Expression schema written to: {exprPath}");

// ── Tag schema (new) ───────────────────────────────────────────────────────
WriteTagSchema(tagsPath);
Console.WriteLine($"Tag schema written to:        {tagsPath}");
return 0;

// ─────────────────────────────────────────────────────────────────────────────
// Scans the Rpg assembly for every static class whose name ends with "Tags",
// collects all  public static readonly Tag<T>  fields, groups them by T, and
// writes a JSON schema with one $def per type (e.g. DamageTypeTag, BodyTag).
// ─────────────────────────────────────────────────────────────────────────────
static void WriteTagSchema(string outputPath)
{
    var rpgAssembly = typeof(Tag<>).Assembly;
    var tagOpenType = typeof(Tag<>);

    // typeName -> list of tag string values
    var tagsByType = new Dictionary<string, List<string>>();

    // Find all static classes ending in "Tags"
    foreach (var type in rpgAssembly.GetTypes())
    {
        if (!type.IsClass || !type.IsAbstract || !type.IsSealed) continue; // static classes
        if (!type.Name.EndsWith("Tags")) continue;

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var fieldType = field.FieldType;
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != tagOpenType)
                continue;

            var genericArg = fieldType.GetGenericArguments()[0];
            var typeName = genericArg.Name; // e.g. "DamageType", "BodyPart", "Body", "Skill"

            var tagInstance = field.GetValue(null);
            if (tagInstance == null) continue;

            var nameField = fieldType.GetField("Name");
            if (nameField == null) continue;

            var tagName = (string?)nameField.GetValue(tagInstance);
            if (tagName == null) continue;

            if (!tagsByType.TryGetValue(typeName, out var list))
            {
                list = new List<string>();
                tagsByType[typeName] = list;
            }

            list.Add(tagName);
        }
    }

    // Build JSON schema
    var defs = new Dictionary<string, object>();
    foreach (var (typeName, tags) in tagsByType.OrderBy(kv => kv.Key))
    {
        tags.Sort(StringComparer.Ordinal);
        defs[$"{typeName}Tag"] = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["enum"] = tags,
            ["description"] = $"Known tags for {typeName}."
        };
    }

    var schema = new Dictionary<string, object>
    {
        ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
        ["$id"] = "_tags.schema.json",
        ["title"] = "Tags",
        ["description"] = "Auto-generated tag enums extracted from *Tags static classes in the Rpg assembly.",
        ["$defs"] = defs
    };

    var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    File.WriteAllText(outputPath, json + Environment.NewLine);
}
