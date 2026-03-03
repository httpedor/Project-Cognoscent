using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComponentGenerator;

[Generator]
public class ExprSourceGenerator : ISourceGenerator
{
    private const string ExprOpAttributeMetadataName = "Rpg.Scripting.ExprOpAttribute";
    private const string ExprParamAttributeMetadataName = "Rpg.Scripting.ExprParamAttribute";

    // Category enum values (must match Rpg.Scripting.ExprCategory)
    private const int CategoryNumber = 0;
    private const int CategoryCondition = 1;
    private const int CategoryEffect = 2;
    private const int CategorySelector = 3;
    private const int CategoryString = 4;

    private static readonly DiagnosticDescriptor MethodMustBeStatic = new(
        id: "EXPR001",
        title: "ExprOp method must be static",
        messageFormat: "Method '{0}' marked with [ExprOp] must be static",
        category: "ExprSourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOpName = new(
        id: "EXPR002",
        title: "Duplicate op name",
        messageFormat: "Op name '{0}' is registered by both '{1}' and '{2}'",
        category: "ExprSourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ExprReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not ExprReceiver receiver)
            return;

        var exprOpSymbol = context.Compilation.GetTypeByMetadataName(ExprOpAttributeMetadataName);
        if (exprOpSymbol is null)
            return;

        var exprParamSymbol = context.Compilation.GetTypeByMetadataName(ExprParamAttributeMetadataName);

        // Collect all methods with [ExprOp]
        var registrations = new List<ExprOpRegistration>();

        foreach (var methodDecl in receiver.CandidateMethods)
        {
            var model = context.Compilation.GetSemanticModel(methodDecl.SyntaxTree);
            if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
                continue;

            var opAttrs = methodSymbol.GetAttributes()
                .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, exprOpSymbol))
                .ToList();

            if (opAttrs.Count == 0)
                continue;

            // Validate: must be static
            if (!methodSymbol.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MethodMustBeStatic,
                    methodDecl.Identifier.GetLocation(),
                    methodSymbol.ToDisplayString()));
                continue;
            }

            // Collect param attributes
            var paramAttrs = new List<ExprParamInfo>();
            if (exprParamSymbol is not null)
            {
                foreach (var attr in methodSymbol.GetAttributes()
                    .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, exprParamSymbol)))
                {
                    if (attr.ConstructorArguments.Length >= 2)
                    {
                        var name = attr.ConstructorArguments[0].Value as string ?? "";
                        var schemaRef = attr.ConstructorArguments[1].Value as string ?? "";
                        var required = false;
                        string description = null;

                        foreach (var named in attr.NamedArguments)
                        {
                            if (named.Key == "Required" && named.Value.Value is bool b)
                                required = b;
                            else if (named.Key == "Description" && named.Value.Value is string d)
                                description = d;
                        }

                        paramAttrs.Add(new ExprParamInfo(name, schemaRef, required, description));
                    }
                }
            }

            // Process each [ExprOp] on this method
            foreach (var attr in opAttrs)
            {
                if (attr.ConstructorArguments.Length < 2)
                    continue;

                var categoryValue = (int)attr.ConstructorArguments[0].Value;
                var opNamesArg = attr.ConstructorArguments[1];

                var opNames = new List<string>();
                if (opNamesArg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in opNamesArg.Values)
                    {
                        if (item.Value is string s)
                            opNames.Add(s);
                    }
                }

                if (opNames.Count == 0)
                    continue;

                var containingType = methodSymbol.ContainingType;
                var fullyQualifiedType = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var methodName = methodSymbol.Name;

                registrations.Add(new ExprOpRegistration(
                    categoryValue,
                    opNames.ToArray(),
                    fullyQualifiedType,
                    methodName,
                    paramAttrs.ToArray()));
            }
        }

        if (registrations.Count == 0)
            return;

        // Check for duplicate op names within the same category
        var opsByCategory = registrations
            .SelectMany(r => r.OpNames.Select(op => new { Op = op.ToLowerInvariant(), Reg = r }))
            .GroupBy(x => new { x.Op, x.Reg.Category });

        var seenOps = new Dictionary<(int category, string op), string>();
        foreach (var reg in registrations)
        {
            foreach (var op in reg.OpNames)
            {
                var key = (reg.Category, op.ToLowerInvariant());
                if (seenOps.TryGetValue(key, out var existing))
                {
                    var current = $"{reg.FullyQualifiedType}.{reg.MethodName}";
                    if (existing != current)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateOpName,
                            Location.None,
                            op, existing, current));
                    }
                }
                else
                {
                    seenOps[key] = $"{reg.FullyQualifiedType}.{reg.MethodName}";
                }
            }
        }

        GenerateRegistry(context, registrations);
        GenerateSchema(context, registrations);
    }

    private static void GenerateRegistry(
        GeneratorExecutionContext context,
        List<ExprOpRegistration> registrations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        sb.AppendLine("namespace Rpg.Scripting;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated dispatch registry for expression ops.");
        sb.AppendLine("/// Maps JSON op names to their compile methods.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ExprRegistry");
        sb.AppendLine("{");

        // Group by category
        var categories = new (int id, string name, string returnType, string dictType)[]
        {
            (CategoryNumber, "Number", "global::Rpg.Scripting.Expr<float>", "global::Rpg.Scripting.Expr<float>"),
            (CategoryCondition, "Condition", "global::Rpg.Scripting.Expr<bool>", "global::Rpg.Scripting.Expr<bool>"),
            (CategoryEffect, "Effect", "global::Rpg.Scripting.EffectExpr", "global::Rpg.Scripting.EffectExpr"),
            (CategorySelector, "Selector", "global::Rpg.Scripting.Expr<global::Rpg.Entities.Entity?>", "global::Rpg.Scripting.Expr<global::Rpg.Entities.Entity?>"),
            (CategoryString, "String", "global::Rpg.Scripting.Expr<string>", "global::Rpg.Scripting.Expr<string>"),
        };

        foreach (var (catId, catName, returnType, dictType) in categories)
        {
            var catRegs = registrations.Where(r => r.Category == catId).ToList();

            sb.AppendLine($"    private static readonly Dictionary<string, Func<JsonElement, {dictType}>> _{catName}Ops =");
            sb.AppendLine($"        new Dictionary<string, Func<JsonElement, {dictType}>>(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            foreach (var reg in catRegs)
            {
                foreach (var op in reg.OpNames)
                {
                    sb.AppendLine($"            [\"{EscapeString(op)}\"] = {reg.FullyQualifiedType}.{reg.MethodName},");
                }
            }

            sb.AppendLine("        };");
            sb.AppendLine();

            // Compile method
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Looks up and invokes the registered compile method for the given {catName} op name.");
            sb.AppendLine($"    /// Returns null if the op name is not registered.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static {returnType}? Compile{catName}(JsonElement obj, string op)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (_{catName}Ops.TryGetValue(op, out var factory))");
            sb.AppendLine("            return factory(obj);");
            sb.AppendLine("        return null;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // HasOp method
            sb.AppendLine($"    public static bool Has{catName}Op(string op) => _{catName}Ops.ContainsKey(op);");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        context.AddSource("ExprRegistry.g.cs", sb.ToString());
    }

    private static void GenerateSchema(
        GeneratorExecutionContext context,
        List<ExprOpRegistration> registrations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Rpg.Scripting;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated JSON schema for expression ops.");
        sb.AppendLine("/// Call <see cref=\"WriteToFile\"/> to write the schema to disk.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ExprJsonSchema");
        sb.AppendLine("{");

        // Build schema JSON as a const string
        var schemaJson = BuildSchemaJson(registrations);
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The complete JSON schema for all registered expression ops.");
        sb.AppendLine("    /// </summary>");
        sb.Append("    public const string Schema = @\"");
        sb.Append(schemaJson.Replace("\"", "\"\""));
        sb.AppendLine("\";");
        sb.AppendLine();

        // WriteToFile helper
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Writes the expression JSON schema to the specified file path.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void WriteToFile(string path)");
        sb.AppendLine("    {");
        sb.AppendLine("        System.IO.File.WriteAllText(path, Schema);");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        context.AddSource("ExprJsonSchema.g.cs", sb.ToString());
    }

    private static string BuildSchemaJson(List<ExprOpRegistration> registrations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",");
        sb.AppendLine("  \"$id\": \"_expr.schema.json\",");
        sb.AppendLine("  \"title\": \"Shared expression definitions\",");
        sb.AppendLine("  \"description\": \"Reusable $defs for all expression types used by ExpressionCompiler. Auto-generated — do not edit manually.\",");
        sb.AppendLine("  \"$defs\": {");

        // Helper: map category id to its schema definition name, description, & literal base types
        var categorySchemas = new (int id, string defName, string description, bool hasIfConditional, string[] baseTypes)[]
        {
            (CategoryNumber, "NumberExpr",
             "A numeric expression (Expr<float>). Can be a constant, dice/range shorthand, indexed variable, or an object with an 'op' field.",
             true,
             new[]
             {
                 "{ \"type\": \"number\", \"description\": \"Numeric constant, e.g. 3.5\" }",
                 "{ \"type\": \"string\", \"description\": \"Dice notation ('1d6', '2D8', '1-4', '3:6'), or indexed variable ('$0', '$1', ...).\" }"
             }),
            (CategoryCondition, "ConditionExpr",
             "A boolean/condition expression (Expr<bool>).",
             true,
             new[]
             {
                 "{ \"type\": \"boolean\", \"description\": \"Constant true or false.\" }",
                 "{ \"type\": \"null\", \"description\": \"Equivalent to false.\" }",
                 "{ \"type\": \"number\", \"minimum\": 0, \"description\": \"Probability check: 0-1 as a fraction, >1 treated as percentage divided by 100.\" }",
                 "{ \"type\": \"string\", \"description\": \"Constant 'true'/'false', probability as 'N%', or indexed variable '$N'.\" }"
             }),
            (CategoryEffect, "EffectExpr",
             "An effect expression (EffectExpr).",
             false,
             new[]
             {
                 "{ \"type\": [\"null\", \"boolean\"], \"enum\": [null, false], \"description\": \"No effect.\" }"
             }),
            (CategorySelector, "SelectorExpr",
             "Selects an Entity (Expr<Entity?>). Can reference caller, target, target_part, or a variable.",
             true,
             new[]
             {
                 "{ \"type\": \"string\", \"enum\": [\"self\", \"caller\", \"target\", \"target_part\"], \"description\": \"Named fixed selector.\" }",
                 "{ \"type\": \"string\", \"pattern\": \"^\\\\$[0-9]+$\", \"description\": \"Indexed variable, e.g. '$0'.\" }",
                 "{ \"type\": [\"null\", \"boolean\"], \"enum\": [null, false], \"description\": \"No entity / null selector.\" }"
             }),
            (CategoryString, "StringExpr",
             "A string expression (Expr<string>). Can be a literal, indexed variable, or a concat operation.",
             true,
             new[]
             {
                 "{ \"type\": \"string\", \"description\": \"String literal or '$N' indexed variable.\" }"
             }),
        };

        for (int ci = 0; ci < categorySchemas.Length; ci++)
        {
            var (catId, defName, description, hasIfConditional, baseTypes) = categorySchemas[ci];
            var catRegs = registrations.Where(r => r.Category == catId).ToList();

            sb.Append("    \"").Append(defName).AppendLine("\": {");
            sb.Append("      \"description\": \"").Append(EscapeJsonString(description)).AppendLine("\",");
            sb.AppendLine("      \"oneOf\": [");

            // Base types (literal forms)
            foreach (var bt in baseTypes)
            {
                sb.Append("        ").Append(bt).AppendLine(",");
            }

            // Conditional if (available for all categories except Effect)
            if (hasIfConditional)
            {
                sb.AppendLine("        {");
                sb.AppendLine("          \"type\": \"object\",");
                sb.AppendLine("          \"description\": \"Conditional expression (if/then/else).\",");
                sb.AppendLine("          \"required\": [\"op\", \"condition\", \"true\", \"false\"],");
                sb.AppendLine("          \"properties\": {");
                sb.AppendLine("            \"op\": { \"const\": \"if\" },");
                sb.AppendLine("            \"condition\": { \"$ref\": \"#/$defs/ConditionExpr\" },");
                sb.Append("            \"true\": { \"$ref\": \"#/$defs/").Append(defName).AppendLine("\" },");
                sb.Append("            \"false\": { \"$ref\": \"#/$defs/").Append(defName).AppendLine("\" }");
                sb.AppendLine("          },");
                sb.AppendLine("          \"unevaluatedProperties\": false");
                sb.AppendLine("        },");
            }

            // Group registrations by method (multiple [ExprOp] on same method → same params)
            var regsByMethod = catRegs
                .GroupBy(r => $"{r.FullyQualifiedType}.{r.MethodName}")
                .ToList();

            for (int ri = 0; ri < regsByMethod.Count; ri++)
            {
                var group = regsByMethod[ri];
                var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                var paramInfos = group.First().Params;

                sb.AppendLine("        {");
                sb.AppendLine("          \"type\": \"object\",");

                // Required properties
                var opPropertyField = catId == CategoryEffect ? "effect" : "op";
                var requiredProps = new List<string> { opPropertyField };
                requiredProps.AddRange(paramInfos.Where(p => p.Required).Select(p => p.Name));
                sb.Append("          \"required\": [");
                sb.Append(string.Join(", ", requiredProps.Select(p => $"\"{EscapeJsonString(p)}\"")));
                sb.AppendLine("],");

                // Properties
                sb.AppendLine("          \"properties\": {");

                // op/effect enum
                sb.Append("            \"").Append(opPropertyField).Append("\": { \"enum\": [");
                sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                if (paramInfos.Length > 0)
                    sb.AppendLine("] },");
                else
                    sb.AppendLine("] }");

                // Params
                for (int pi = 0; pi < paramInfos.Length; pi++)
                {
                    var param = paramInfos[pi];
                    sb.Append("            \"").Append(EscapeJsonString(param.Name)).Append("\": ");
                    sb.Append(SchemaRefToJson(param.SchemaRef, param.Description));
                    if (pi < paramInfos.Length - 1)
                        sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("          },");
                sb.AppendLine("          \"unevaluatedProperties\": false");

                sb.Append("        }");
                if (ri < regsByMethod.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("      ]");
            sb.Append("    }");
            // Comma after each category def — static defs follow, so always comma here
            sb.AppendLine(",");
        }

        // Static defs shared across schemas
        sb.AppendLine("    \"StatModifierType\": {");
        sb.AppendLine("      \"type\": \"string\",");
        sb.AppendLine("      \"enum\": [\"Flat\", \"FlatPostMods\", \"Percent\", \"Multiplier\", \"Capmax\", \"Capmin\", \"OverrideBase\", \"OverrideFinal\"],");
        sb.AppendLine("      \"description\": \"How the modifier value is applied to the stat. Case-insensitive at runtime.\"");
        sb.AppendLine("    },");
        sb.AppendLine("    \"ArgumentType\": {");
        sb.AppendLine("      \"type\": \"string\",");
        sb.AppendLine("      \"enum\": [\"entity\", \"position\", \"bodypart\", \"item\", \"boolean\"],");
        sb.AppendLine("      \"description\": \"A skill argument type identifier.\"");
        sb.AppendLine("    }");

        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string SchemaRefToJson(string schemaRef, string description)
    {
        string descPart = description != null
            ? $", \"description\": \"{EscapeJsonString(description)}\""
            : "";

        if (schemaRef.EndsWith("[]"))
        {
            var itemRef = schemaRef.Substring(0, schemaRef.Length - 2);
            var itemSchema = SchemaRefToJsonInner(itemRef);
            return $"{{ \"type\": \"array\", \"items\": {itemSchema}{descPart} }}";
        }

        var inner = SchemaRefToJsonInner(schemaRef);
        if (!string.IsNullOrEmpty(descPart) && inner.StartsWith("{"))
        {
            // Insert description into the existing object
            return inner.TrimEnd('}', ' ') + descPart + " }";
        }
        return inner;
    }

    private static string SchemaRefToJsonInner(string schemaRef)
    {
        switch (schemaRef)
        {
            case "numberExpr": return "{ \"$ref\": \"#/$defs/NumberExpr\" }";
            case "conditionExpr": return "{ \"$ref\": \"#/$defs/ConditionExpr\" }";
            case "effectExpr": return "{ \"$ref\": \"#/$defs/EffectExpr\" }";
            case "selectorExpr": return "{ \"$ref\": \"#/$defs/SelectorExpr\" }";
            case "stringExpr": return "{ \"$ref\": \"#/$defs/StringExpr\" }";
            case "string": return "{ \"type\": \"string\" }";
            case "number": return "{ \"type\": \"number\" }";
            case "boolean": return "{ \"type\": \"boolean\" }";
            case "integer": return "{ \"type\": \"integer\" }";
            default: return "{}";
        }
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string EscapeJsonString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class ExprReceiver : ISyntaxReceiver
    {
        public List<MethodDeclarationSyntax> CandidateMethods { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Collect methods that have at least one attribute (we'll filter later)
            if (syntaxNode is MethodDeclarationSyntax mds
                && mds.AttributeLists.Count > 0
                && mds.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                CandidateMethods.Add(mds);
            }
        }
    }

    private class ExprParamInfo
    {
        public string Name { get; }
        public string SchemaRef { get; }
        public bool Required { get; }
        public string Description { get; }

        public ExprParamInfo(string name, string schemaRef, bool required, string description)
        {
            Name = name;
            SchemaRef = schemaRef;
            Required = required;
            Description = description;
        }
    }

    private class ExprOpRegistration
    {
        public int Category { get; }
        public string[] OpNames { get; }
        public string FullyQualifiedType { get; }
        public string MethodName { get; }
        public ExprParamInfo[] Params { get; }

        public ExprOpRegistration(int category, string[] opNames, string fullyQualifiedType, string methodName, ExprParamInfo[] paramInfos)
        {
            Category = category;
            OpNames = opNames;
            FullyQualifiedType = fullyQualifiedType;
            MethodName = methodName;
            Params = paramInfos;
        }
    }
}
