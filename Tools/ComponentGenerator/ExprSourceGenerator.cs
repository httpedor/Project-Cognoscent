using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private const int CategoryEntity = 3;
    private const int CategoryComponent = 4;
    private const int CategoryString = 5;

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

    private static List<ExprOpRegistration> _allRegistrations = new();

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
                        var schemaTypeArg = attr.ConstructorArguments[1];
                        if (schemaTypeArg.Value is not ITypeSymbol schemaTypeSymbol)
                            continue;

                        var required = false;
                        string description = null!;
                        string? subType = null;
                        foreach (var named in attr.NamedArguments)
                        {
                            if (named.Key == "Required" && named.Value.Value is bool b)
                                required = b;
                            else if (named.Key == "Description" && named.Value.Value is string d)
                                description = d;
                            else if (named.Key == "SubType" && named.Value.Value is string st)
                                subType = st;
                        }

                        switch (schemaTypeSymbol.SpecialType)
                        {
                            case SpecialType.System_UInt16:
                            case SpecialType.System_UInt32:
                            case SpecialType.System_UInt64:
                            case SpecialType.System_Int16:
                            case SpecialType.System_Int32:
                            case SpecialType.System_Int64:
                            case SpecialType.System_Double:
                                schemaTypeSymbol = context.Compilation.GetTypeByMetadataName("System.Single")!;
                                break;
                        }

                        if (string.IsNullOrEmpty(subType))
                        {
                            Stack<ITypeSymbol> symbols = new();
                            symbols.Push(schemaTypeSymbol);
                            while (symbols.Count > 0)
                            {
                                var current = symbols.Pop();
                                if (current.Name == "Component")
                                {
                                    subType = schemaTypeSymbol.Name;
                                    schemaTypeSymbol = current;
                                    break;
                                }
                                else if (current.Name == "Tag" && current is INamedTypeSymbol nts && nts.TypeArguments.Length == 1)
                                {
                                    subType = $"{nts.TypeArguments[0].Name}Tag";
                                    schemaTypeSymbol = current;
                                    break;
                                }
                                if (current.BaseType != null)
                                    symbols.Push(current.BaseType);
                            }
                        }

                        paramAttrs.Add(new ExprParamInfo(name, schemaTypeSymbol, required, description!, subType));
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

                string? genericType = null;
                bool isArray = false;
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "GenericType" && named.Value.Value is string gt)
                        genericType = gt;
                    if (named.Key == "IsArray" && named.Value.Value is bool ia)
                        isArray = ia;
                }

                registrations.Add(new ExprOpRegistration(
                    categoryValue,
                    opNames.ToArray(),
                    fullyQualifiedType,
                    methodName,
                    paramAttrs.ToArray(),
                    isArray,
                    genericType));
            }
        }

        if (registrations.Count == 0)
            return;

        _allRegistrations = registrations;

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

        GenerateRegistry(context);
        GenerateSchema(context);
    }

    private static int CategoryFromType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Double:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Single:
                return CategoryNumber;
            case SpecialType.System_Boolean:
                return CategoryCondition;
            case SpecialType.System_String:
                return CategoryString;
            case SpecialType.System_Collections_Generic_IList_T:
            case SpecialType.System_Collections_Generic_IReadOnlyList_T:
            case SpecialType.System_Collections_Generic_ICollection_T:
            case SpecialType.System_Collections_Generic_IEnumerable_T:
                if (type is INamedTypeSymbol nts && nts.TypeArguments.Length == 1)
                {
                    var elementType = nts.TypeArguments[0];
                    return CategoryFromType(elementType);
                }
                break;
        }
        if (type.Name == "Entity" && type.ContainingNamespace.ToDisplayString() == "Rpg.Entities")
            return CategoryEntity;
        if (type.Name == "Component" && type.ContainingNamespace.ToDisplayString() == "Rpg.Entities")
            return CategoryComponent;
        if (type.TypeKind == TypeKind.Enum)
            return CategoryString; // Enums are treated as strings in the schema, but we can still generate typed component exprs for them based on their name
        return -1; // Unknown category
    }

    private static void GenerateRegistry(
        GeneratorExecutionContext context)
    {
        var registrations = _allRegistrations;
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

        sb.AppendLine("    public sealed class ExprBuilderInfo");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Category { get; }");
        sb.AppendLine("        public bool IsArray { get; }");
        sb.AppendLine("        public Func<JsonElement, object?> Builder { get; }");
        sb.AppendLine("        public ExprBuilderInfo(string category, bool isArray, Func<JsonElement, object?> builder)");
        sb.AppendLine("        {");
        sb.AppendLine("            Category = category ?? throw new ArgumentNullException(nameof(category));");
        sb.AppendLine("            IsArray = isArray;");
        sb.AppendLine("            Builder = builder ?? throw new ArgumentNullException(nameof(builder));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Group by category
        var categories = new (int id, string name, string returnType)[]
        {
            (CategoryNumber, "Number", "float"),
            (CategoryCondition, "Condition", "bool"),
            (CategoryEffect, "Effect", "void"),
            (CategoryEntity, "Entity", "global::Rpg.Entities.Entity?"),
            (CategoryComponent, "Component", "global::Rpg.Entities.Component?"),
            (CategoryString, "String", "string"),
        };

        foreach (var (catId, catName, exprType) in categories)
        {
            var catRegs = registrations.Where(r => r.Category == catId && !r.IsArray).ToList();
            var catArrayRegs = registrations.Where(r => r.Category == catId && r.IsArray).ToList();
            string returnType = "global::Rpg.Scripting.Expr<" + exprType + ">";
            string arrayReturnType = "global::Rpg.Scripting.ArrayExpr<" + exprType + ">";
            bool hasArray = catArrayRegs.Any();
            if (exprType == "void")
            {
                returnType = "global::Rpg.Scripting.EffectExpr";
                hasArray = false;
            }

            sb.AppendLine($"    private static readonly Dictionary<string, Func<JsonElement, {returnType}>> _{catName}Ops =");
            sb.AppendLine($"        new Dictionary<string, Func<JsonElement, {returnType}>>(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            foreach (var reg in catRegs)
            {
                foreach (var op in reg.OpNames)
                {
                    sb.AppendLine($"            [\"{EscapeString(op)}\"] = {reg.FullyQualifiedType}.{reg.MethodName},");
                }
            }

            sb.AppendLine("        };");
            if (hasArray)
            {
                sb.AppendLine($"    private static readonly Dictionary<string, Func<JsonElement, {arrayReturnType}>> _{catName}ArrayOps =");
                sb.AppendLine($"        new Dictionary<string, Func<JsonElement, {arrayReturnType}>>(StringComparer.OrdinalIgnoreCase)");
                sb.AppendLine("        {");

                foreach (var reg in catArrayRegs)
                {
                    foreach (var op in reg.OpNames)
                    {
                        sb.AppendLine($"            [\"{EscapeString(op)}\"] = {reg.FullyQualifiedType}.{reg.MethodName},");
                    }
                }

                sb.AppendLine("        };");
            }
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

            // Dynamic Registration
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Registers a new {catName} op name and its compile method at runtime.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static void Register{catName}Op(string op, Func<JsonElement, {returnType}> factory)");
            sb.AppendLine("    {");
            sb.AppendLine($"        _{catName}Ops[op] = factory;");
            sb.AppendLine("    }");


            //CompileArray method
            if (exprType != "void")
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Compiles an array expression of type {returnType}.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    public static {arrayReturnType}? Compile{catName}Array(JsonElement obj, string op)");
                sb.AppendLine("    {");
                if (hasArray)
                {
                    sb.AppendLine($"        if (_{catName}ArrayOps.TryGetValue(op, out var factory))");
                    sb.AppendLine("            return factory(obj);");
                    sb.AppendLine("        return null;");
                }
                else
                {
                    sb.AppendLine("        return null; // No array ops registered for this category");
                }
                sb.AppendLine("    }");

                // Dynamic Registration for array ops
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Registers a new array op name and its compile method for {returnType} at runtime.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    public static void Register{catName}ArrayOp(string op, Func<JsonElement, {arrayReturnType}> factory)");
                sb.AppendLine("    {");
                if (hasArray)
                {
                    sb.AppendLine($"        _{catName}ArrayOps[op] = factory;");
                }
                else
                {
                    sb.AppendLine("        throw new InvalidOperationException(\"No array ops supported for this category\");");
                }
                sb.AppendLine("    }");
            }

            // HasOp method
            sb.AppendLine($"    public static bool Has{catName}Op(string op) => _{catName}Ops.ContainsKey(op);");
            sb.Append($"    public static bool Has{catName}ArrayOp(string op) => ");
            if (hasArray)
                sb.AppendLine($"_{catName}ArrayOps.ContainsKey(op);");
            else
                sb.AppendLine("false;");
            sb.AppendLine();
        }
        sb.AppendLine("    public static IReadOnlyList<ExprBuilderInfo> GetAllBuilders(string op)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (op == null) throw new ArgumentNullException(nameof(op));");
        sb.AppendLine("        var result = new List<ExprBuilderInfo>();");

        foreach (var cat in categories)
        {
            sb.AppendLine($"        // Check {cat.name} ops");
            sb.AppendLine($"        if (Has{cat.name}Op(op))");
            sb.AppendLine($"            result.Add(new ExprBuilderInfo(\"{cat.name}\", false, e => Compile{cat.name}(e, op)!));");
            if (cat.returnType != "void")
            {
                sb.AppendLine($"        if (Has{cat.name}ArrayOp(op))");
                sb.AppendLine($"            result.Add(new ExprBuilderInfo(\"{cat.name}\", true, e => Compile{cat.name}Array(e, op)!));");
            }
        }
        sb.AppendLine("        if (HasNumberOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Number\", false, e => CompileNumber(e, op)!));");
        sb.AppendLine("        if (HasNumberArrayOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Number\", true, e => CompileNumberArray(e, op)!));");

        sb.AppendLine("        if (HasConditionOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Condition\", false, e => CompileCondition(e, op)!));");
        sb.AppendLine("        if (HasConditionArrayOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Condition\", true, e => CompileConditionArray(e, op)!));");

        sb.AppendLine("        if (HasEffectOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Effect\", false, e => CompileEffect(e, op)!));");

        sb.AppendLine("        if (HasEntityOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Entity\", false, e => CompileEntity(e, op)!));");
        sb.AppendLine("        if (HasEntityArrayOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Entity\", true, e => CompileEntityArray(e, op)!));");

        sb.AppendLine("        if (HasComponentOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Component\", false, e => CompileComponent(e, op)!));");
        sb.AppendLine("        if (HasComponentArrayOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"Component\", true, e => CompileComponentArray(e, op)!));");

        sb.AppendLine("        if (HasStringOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"String\", false, e => CompileString(e, op)!));");
        sb.AppendLine("        if (HasStringArrayOp(op))");
        sb.AppendLine("            result.Add(new ExprBuilderInfo(\"String\", true, e => CompileStringArray(e, op)!));");

        sb.AppendLine("        return result;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        context.AddSource("ExprRegistry.g.cs", sb.ToString());
    }

    private static void GenerateSchema(
        GeneratorExecutionContext context)
    {
        var registrations = _allRegistrations;

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

    private static void WriteMetaOp(StringBuilder sb, string defName, string op)
    {
        var lines = op.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        foreach (var line in lines)
        {
            sb.Append("        ");
            if (i == lines.Length - 1)
                sb.AppendLine(line.Replace("<def>", defName) + ",");
            else
                sb.AppendLine(line.Replace("<def>", defName));
            i++;
        }
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

        // AnyExpr collector: all generated expr defs (including generated typed component exprs)
        var anyExprDefs = new List<string>();
        var anyExprArrayDefs = new List<string>();

        var metaOps = new string[]
        {
            """
            {
              "type": "object",
              "description": "Conditional expression (if/then/else).",
              "required": ["op", "condition", "true", "false"],
              "properties": {
                "op": { "const": "if" },
                "condition": { "$ref": "#/$defs/ConditionExpr" },
                "true": { "$ref": "#/$defs/<def>" },
                "false": { "$ref": "#/$defs/<def>" }
              },
              "unevaluatedProperties": true
            }
            """,
            """
            {
              "type": "object",
              "description": "Runs the inner expression with the given variables added to the context. The variables are added from 0. If the EvalContext already has variables, the new ones will be added at 0 and shift the existing ones up.",
              "required": ["op", "variables", "expression"],
              "properties": {
                "op": { "enum": ["with_vars", "with_var", "with_variables"] },
                "variables": { "$ref": "#/$defs/BaseExpr" },
                "expression": { "$ref": "#/$defs/<def>" }
              },
              "unevaluatedProperties": true
            }
            """,
            """
            {
              "type": "object",
              "description": "Chains multiple if-else conditions together.",
              "required": ["op", "branches"],
              "properties": {
                "op": { "enum": ["if_chain"] },
                "branches": {
                    "type": "array",
                    "description": "Array of condition/expr pairs. The first condition that evaluates to true will have its corresponding expr returned. The last branch can have a null condition to serve as a default case.",
                    "items": {
                        "type": "object",
                        "required": ["condition", "expr"],
                        "properties": {
                            "condition": { "$ref": "#/$defs/ConditionExpr" },
                            "expr": { "$ref": "#/$defs/<def>" }
                        },
                        "unevaluatedProperties": true
                    },
                    "minItems": 1
                },
                "default": { "$ref": "#/$defs/<def>" }
              },
              "unevaluatedProperties": true
            }
            """,
            """
            {
                "type": "object",
                "description": "Returns the specified expr based on a number. Switch-Case for numbers.",
                "required": ["op", "value", "cases"],
                "properties": {
                    "op": { "enum": ["switch_number"] },
                    "value": { "$ref": "#/$defs/NumberExpr" },
                    "cases": {
                        "type": "object",
                        "description": "Object with the key being the target number, and the value being the resulting expr",
                        "additionalProperties": {
                            "$ref": "#/$defs/<def>"
                        }
                    },
                    "default": { "$ref": "#/$defs/<def>" }
                },
                "unevaluatedProperties": true
            }
            """,
            """
            {
                "type": "object",
                "description": "Switch-Case for number RANGES.",
                "required": ["op", "value", "cases"],
                "properties": {
                    "op": { "enum": ["switch_range"] },
                    "value": { "$ref": "#/$defs/NumberExpr" },
                    "cases": {
                        "type": "array",
                        "description": "Array of range/expr pairs. The first range that matches the input number will have its corresponding expr returned. The last case can have a null range to serve as a default case.",
                        "items": {
                            "type": "object",
                            "required": ["min", "max", "expr"],
                            "properties": {
                                "min": {"type": "number"},
                                "max": {"type": "number"},
                                "expr": { "$ref": "#/$defs/<def>" }
                            },
                            "unevaluatedProperties": true
                        },
                        "minItems": 1
                    },
                    "default": { "$ref": "#/$defs/<def>" }
                },
                "unevaluatedProperties": true
            }
            """,
            """
            {
                "type": "object",
                "description": "Runs an expression from a library",
                "required": ["op", "id", "args"],
                "properties": {
                    "op": { "enum": ["function", "run_function", "run_expr", "call_function", "call_expr"] },
                    "library": { "$ref": "#/$defs/StringExpr" },
                    "id": { "$ref": "#/$defs/StringExpr" },
                    "args": { "$ref": "#/$defs/AnyExprArray" }
                },
                "unevaluatedProperties": true
            }
            """
        };
        var arrayMetaOps = new string[]
        {
            """
            {
              "type": "array",
              "description": "Manually typed array of <def>.",
              "items": { "$ref": "#/$defs/<def>" }
            }
            """,
            """
            {
              "type": "object",
              "description": "Single <def> that will create an array with one element.",
              "$ref": "#/$defs/<def>"
            }
            """,
            """
            {
              "type": "object",
              "description": "Iterates over the elements of an array and transforms them based on an expression.",
              "required": ["op", "values", "expression"],
              "properties": {
                "op": { "enum": ["map"] },
                "values": { "$ref": "#/$defs/BaseExpr" },
                "expression": { "$ref": "#/$defs/<def>" }
              },
              "unevaluatedProperties": true
            }
            """,
            """
            {
              "type": "object",
              "description": "Filters the elements of an array based on a condition expression.",
              "required": ["op", "values", "condition"],
              "properties": {
                "op": { "enum": ["filter"] },
                "values": { "$ref": "#/$defs/AnyExprArray" },
                "condition": { "$ref": "#/$defs/ConditionExpr" }
              },
              "unevaluatedProperties": true
            }
            """,
            """
            {
              "type": "object",
              "description": "Appends two or more arrays.",
              "required": ["op", "arrays"],
              "properties": {
                "op": { "enum": ["concat", "append", "join"] },
                "arrays": {
                  "type": "array",
                  "description": "Two or more arrays to concatenate.",
                  "items": { "$ref": "#/$defs/AnyExprArray" },
                  "minItems": 2
                }
              }
            }
            """,
            """
            {
                "type": "object",
                "description": "Sorts the elements of an array using a comparison expression. The comparison expression is evaluated with variables $0 (first element) and $1 (second element). It should return true if the first element should come before the second.",
                "required": ["op", "values", "comparison"],
                "properties": {
                    "op": { "enum": ["order", "sort", "order_by"] },
                    "values": { "$ref": "#/$defs/BaseExpr" },
                    "comparison": { "$ref": "#/$defs/ConditionExpr" }
                },
                "unevaluatedProperties": true
            }
            """,
            """
            {
                "type": "object",
                "description": "Selects elements whose computed numeric value is <= the provided 'value', then returns up to 'count' items ordered ascending by the computed value.",
                "required": ["op", "values", "value", "count"],
                "properties": {
                    "op": { "enum": ["max_elements", "max", "top"] },
                    "values": { "$ref": "#/$defs/BaseExpr" },
                    "value": { "$ref": "#/$defs/NumberExpr" },
                    "count": { "$ref": "#/$defs/NumberExpr" }
                },
                "unevaluatedProperties": true
            }
            """,
            """
            {
                "type": "object",
                "description": "Selects elements whose computed numeric value is >= the provided 'value', then returns up to 'count' items ordered descending by the computed value.",
                "required": ["op", "values", "value", "count"],
                "properties": {
                    "op": { "enum": ["min_elements", "min", "bottom"] },
                    "values": { "$ref": "#/$defs/BaseExpr" },
                    "value": { "$ref": "#/$defs/NumberExpr" },
                    "count": { "$ref": "#/$defs/NumberExpr" }
                },
                "unevaluatedProperties": true
            }
            """,
            /*"""
            {
              "type": "object",
              "description": "Runs the inner expression with the given variables added to the context. The variables are added from 0. If the EvalContext already has variables, the new ones will be added at 0 and shift the existing ones up.",
              "required": ["op", "variables", "expression"],
              "properties": {
                "op": { "enum": ["with_vars", "with_var", "with_variables"] },
                "variables": { "$ref": "#/$defs/AnyExprArray" },
                "expression": { "$ref": "#/$defs/<def>" }
              },
              "unevaluatedProperties": true
            }
            """,*/
            """
            {
                "type": "object",
                "description": "Runs an expression from a library",
                "required": ["op", "id", "args"],
                "properties": {
                    "op": { "enum": ["function", "run_function", "run_expr", "call_function", "call_expr"] },
                    "library": { "$ref": "#/$defs/StringExpr" },
                    "id": { "$ref": "#/$defs/StringExpr" },
                    "args": { "$ref": "#/$defs/AnyExprArray" }
                },
                "unevaluatedProperties": true
            }
            """,
            """
            {
              "type": "string",
              "description": "An $N indexed variable, where N is the index of the array in the Variables array.",
              "pattern": "^\\$[0-9]+$"
            }
            """
        };

        // Helper: map category id to its schema definition name, description, & literal base types
        var categorySchemas = new (int id, string defName, string description, string[] baseTypes)[]
        {
            (CategoryNumber, "NumberExpr",
             "A numeric expression (Expr<float>). Can be a constant, dice/range shorthand, indexed variable, or an object with an 'op' field.",
             new[]
             {
                 "{ \"type\": \"number\", \"description\": \"Numeric constant, e.g. 3.5\" }",
                 "{ \"type\": \"string\", \"description\": \"Dice notation ('1d6', '2D8', '1-4', '3:6'), or indexed variable ('$0', '$1', ...).\" }"
             }),
            (CategoryCondition, "ConditionExpr",
             "A boolean/condition expression (Expr<bool>).",
             new[]
             {
                 "{ \"type\": \"boolean\", \"description\": \"Constant true or false.\" }",
                 "{ \"type\": \"null\", \"description\": \"Equivalent to false.\" }",
                 "{ \"type\": \"number\", \"minimum\": 0, \"description\": \"Probability check: 0-1 as a fraction, >1 treated as percentage divided by 100.\" }",
                 "{ \"type\": \"string\", \"description\": \"Constant 'true'/'false', probability as 'N%', or indexed variable '$N'.\" }"
             }),
            (CategoryEffect, "EffectExpr",
             "An effect expression (EffectExpr).",
             new[]
             {
                 "{ \"type\": [\"null\", \"boolean\"], \"enum\": [null, false], \"description\": \"No effect.\" }"
             }),
            (CategoryEntity, "EntityExpr",
             "Selects an Entity (Expr<Entity?>). Can reference caller, target, target_component, or a variable.",
             new[]
             {
                 "{ \"type\": \"string\", \"enum\": [\"self\", \"caller\", \"target\", \"target_component\"], \"description\": \"Named fixed selector.\" }",
                 "{ \"type\": \"string\", \"pattern\": \"^\\\\$[0-9]+$\", \"description\": \"Indexed variable, e.g. '$0'.\" }",
                 "{ \"type\": [\"null\", \"boolean\"], \"enum\": [null, false], \"description\": \"No entity / null selector.\" }"
             }),
            (CategoryComponent, "ComponentExpr",
                "Selects a Component (Expr<Component?>). Can reference caller, target, or a variable.",
                new[]
                {
                    "{ \"type\": \"string\", \"enum\": [\"target_component\"], \"description\": \"Named fixed selector.\" }",
                    "{ \"type\": \"string\", \"pattern\": \"^\\\\$[0-9]+$\", \"description\": \"Indexed variable, e.g. '$0'.\" }",
                    "{ \"type\": [\"null\", \"boolean\"], \"enum\": [null, false], \"description\": \"No component / null selector.\" }"
                }
            ),
            (CategoryString, "StringExpr",
             "A string expression (Expr<string>). Can be a literal, indexed variable, or a concat operation.",
             new[]
             {
                 "{ \"type\": \"string\", \"description\": \"String literal or '$N' indexed variable.\" }"
             }),
        };
        for (int ci = 0; ci < categorySchemas.Length; ci++)
        {
            var (catId, defName, description, baseTypes) = categorySchemas[ci];
            var catRegs = registrations.Where(r => r.Category == catId && !r.IsArray).ToList();
            var catArrayRegs = registrations.Where(r => r.Category == catId && r.IsArray).ToList();

            sb.Append("    \"").Append(defName).AppendLine("\": {");
            sb.Append("      \"description\": \"").Append(EscapeJsonString(description)).AppendLine("\",");
            sb.AppendLine("      \"oneOf\": [");

            // Base types (literal forms)
            foreach (var bt in baseTypes)
            {
                sb.Append("        ").Append(bt).AppendLine(",");
            }

            foreach (var metaOp in metaOps)
            {
                WriteMetaOp(sb, defName, metaOp);
            }
            sb.AppendLine("        {");
            sb.AppendLine("          \"type\": \"object\",");
            sb.AppendLine("          \"properties\": {");
            sb.AppendLine("            \"op\": {");
            sb.AppendLine("              \"type\": \"string\",");
            sb.Append("              \"enum\": [");
            var commonOps = catRegs.SelectMany(r => r.OpNames).Distinct().ToArray();
            sb.Append(string.Join(", ", commonOps.Select(n => $"\"{EscapeJsonString(n)}\"")));
            sb.AppendLine("] }");
            sb.AppendLine("          }");
            //sb.AppendLine("          \"unevaluatedProperties\": false");
            sb.AppendLine("        }");

            // Group registrations by method (multiple [ExprOp] on same method → same params)
            var regsByMethod = catRegs
                .GroupBy(r => $"{r.FullyQualifiedType}.{r.MethodName}")
                .ToList();

            sb.AppendLine("      ]");
            if (regsByMethod.Count > 0)
            {
                sb.AppendLine("      ,\"allOf\": [");

                for (int gi = 0; gi < regsByMethod.Count; gi++)
                {
                    var group = regsByMethod[gi];
                    var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                    var paramInfos = group.First().Params;
                    var opPropertyField = "op";

                    sb.AppendLine("        {");
                    sb.AppendLine("          \"if\": {");
                    sb.AppendLine("            \"properties\": {");
                    sb.Append("              \"").Append(opPropertyField).Append("\": { \"enum\": [");
                    sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                    sb.AppendLine("] } ");
                    sb.AppendLine("            }");
                    sb.AppendLine("          },");
                    sb.AppendLine("          \"then\": {");
                    sb.AppendLine("            \"properties\": {");

                    for (int pi = 0; pi < paramInfos.Length; pi++)
                    {
                        var param = paramInfos[pi];
                        sb.Append("              \"").Append(EscapeJsonString(param.Name)).Append("\": ");
                        sb.Append(SchemaRefToJson(param.SchemaType, param.Description, param.SubType));
                        if (pi < paramInfos.Length - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }

                    sb.AppendLine("            },");

                    var requiredParams = paramInfos.Where(p => p.Required).Select(p => p.Name).ToArray();
                    if (requiredParams.Length > 0)
                    {
                        sb.Append("            \"required\": [");
                        sb.Append(string.Join(", ", requiredParams.Select(p => $"\"{EscapeJsonString(p)}\"")));
                        sb.AppendLine("]");
                    }
                    else
                    {
                        sb.AppendLine("            \"required\": []");
                    }

                    sb.AppendLine("          }");
                    //sb.AppendLine("          \"unevaluatedProperties\": false");
                    sb.Append("        }");
                    if (gi < regsByMethod.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }

                sb.AppendLine("      ]");
            }

            sb.AppendLine("    },");
            if (!anyExprDefs.Contains(defName)) anyExprDefs.Add(defName);
            if (!anyExprArrayDefs.Contains(defName + "Array")) anyExprArrayDefs.Add(defName + "Array");
            sb.Append("    \"").Append(defName + "Array").AppendLine("\": {");
            sb.Append("      \"description\": \"").Append("An array of " + defName).AppendLine("\",");
            sb.AppendLine("      \"oneOf\": [");

            foreach (var arrayMetaOp in arrayMetaOps.Concat(metaOps))
            {
                WriteMetaOp(sb, defName, arrayMetaOp);
            }

            sb.AppendLine("        {");
            sb.AppendLine("          \"type\": \"object\",");
            sb.AppendLine("          \"properties\": {");
            sb.AppendLine("            \"op\": {");
            var arrayOps = catArrayRegs.SelectMany(r => r.OpNames).Distinct().ToArray();
            sb.Append("              \"type\": \"string\", \"enum\": [");
            sb.Append(string.Join(", ", arrayOps.Select(n => $"\"{EscapeJsonString(n)}\"")));
            sb.AppendLine("] }");
            sb.AppendLine("          }");
            sb.AppendLine("        }");
            sb.AppendLine("      ]");
            var arrayRegsByMethod = catArrayRegs
                .GroupBy(r => $"{r.FullyQualifiedType}.{r.MethodName}")
                .ToList();
            if (arrayRegsByMethod.Count > 0)
            {
                sb.AppendLine("      ,\"allOf\": [");
                for (int gi = 0; gi < arrayRegsByMethod.Count; gi++)
                {
                    var group = arrayRegsByMethod[gi];
                    var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                    var paramInfos = group.First().Params;
                    var opPropertyField = "op";

                    sb.AppendLine("        {");
                    sb.AppendLine("          \"if\": {");
                    sb.AppendLine("            \"properties\": {");
                    sb.Append("              \"").Append(opPropertyField).Append("\": { \"enum\": [");
                    sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                    sb.AppendLine("] } ");
                    sb.AppendLine("            }");
                    sb.AppendLine("          },");
                    sb.AppendLine("          \"then\": {");
                    sb.AppendLine("            \"properties\": {");

                    for (int pi = 0; pi < paramInfos.Length; pi++)
                    {
                        var param = paramInfos[pi];
                        sb.Append("              \"").Append(EscapeJsonString(param.Name)).Append("\": ");
                        sb.Append(SchemaRefToJson(param.SchemaType, param.Description, param.SubType));
                        if (pi < paramInfos.Length - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }

                    sb.AppendLine("            },");

                    var requiredParams = paramInfos.Where(p => p.Required).Select(p => p.Name).ToArray();
                    if (requiredParams.Length > 0)
                    {
                        sb.Append("            \"required\": [");
                        sb.Append(string.Join(", ", requiredParams.Select(p => $"\"{EscapeJsonString(p)}\"")));
                        sb.AppendLine("]");
                    }
                    else
                    {
                        sb.AppendLine("            \"required\": []");
                    }

                    sb.AppendLine("          }");
                    //sb.AppendLine("          \"unevaluatedProperties\": false");
                    sb.Append("        }");
                    if (gi < arrayRegsByMethod.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
                sb.AppendLine("      ]");
            }
            sb.Append("    }");
            sb.AppendLine(",");
        }
        var subtypes = registrations
            .SelectMany(r => r.Params)
            .Select(p => (p.SchemaType, p.SubType))
            .Where(p => !string.IsNullOrEmpty(p.SubType))
            .Distinct()
            .ToList();

        foreach (var tuple in subtypes)
        {
            if (tuple.SubType == null)
                continue;

            var subtype = tuple.SubType;
            var baseType = tuple.SchemaType.Name;
            int categoryId = CategoryFromType(tuple.SchemaType);
            if (categoryId == -1)
                continue;
            var matchingOps = registrations
                .Where(r => r.Category == categoryId && r.GenericType != null && string.Equals(r.GenericType, subtype, StringComparison.OrdinalIgnoreCase) && !r.IsArray)
                .GroupBy(r => $"{r.FullyQualifiedType}.{r.MethodName}")
                .ToList();
            var matchingArrayOps = registrations
                .Where(r => r.Category == categoryId && r.GenericType != null && string.Equals(r.GenericType, subtype, StringComparison.OrdinalIgnoreCase) && r.IsArray)
                .GroupBy(r => $"{r.FullyQualifiedType}.{r.MethodName}")
                .ToList();


            sb.AppendLine($"    \"{EscapeJsonString(subtype)}Expr\": {{");
            if (!anyExprDefs.Contains(subtype + "Expr")) anyExprDefs.Add(subtype + "Expr");
            if (!anyExprArrayDefs.Contains(subtype + "ExprArray")) anyExprArrayDefs.Add(subtype + "ExprArray");
            sb.AppendLine($"      \"description\": \"Typed expression for {EscapeJsonString(baseType)}: {EscapeJsonString(subtype)}.\",");
            sb.AppendLine("      \"oneOf\": [");
            foreach (var metaOp in metaOps)
            {
                WriteMetaOp(sb, subtype + "Expr", metaOp);
            }
            if (baseType == "Component")
            {
                sb.AppendLine($"        {{ \"$ref\": \"#/$defs/EntityExpr\" }},");
                sb.AppendLine($"        {{ \"const\": \"target_component\" }},");
            }
            sb.AppendLine("         {");
            sb.AppendLine("          \"type\": \"object\",");
            sb.AppendLine("          \"properties\": {");
            sb.AppendLine("            \"op\": {");
            sb.AppendLine("              \"type\": \"string\",");
            sb.Append("              \"enum\": [");
            for (int ri = 0; ri < matchingOps.Count; ri++)
            {
                var group = matchingOps[ri];
                var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                if (ri < matchingOps.Count - 1)
                    sb.Append(",");
            }
            sb.AppendLine("] }");
            sb.AppendLine("          }");
            sb.AppendLine("        }");


            // From oneOf
            sb.AppendLine("      ]");

            if (matchingOps.Count > 0)
            {
                sb.AppendLine("      ,\"allOf\": [");
                for (int ri = 0; ri < matchingOps.Count; ri++)
                {
                    var group = matchingOps[ri];
                    var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                    var paramInfos = group.First().Params;

                    sb.AppendLine("        {");
                    sb.AppendLine("          \"if\": {");
                    sb.AppendLine("            \"properties\": {");
                    sb.Append("              \"op\": { \"enum\": [");
                    sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                    sb.AppendLine("] } ");
                    sb.AppendLine("            }");
                    sb.AppendLine("          },");
                    sb.AppendLine("          \"then\": {");
                    sb.AppendLine("            \"properties\": {");

                    for (int pi = 0; pi < paramInfos.Length; pi++)
                    {
                        var param = paramInfos[pi];
                        sb.Append("              \"").Append(EscapeJsonString(param.Name)).Append("\": ");
                        sb.Append(SchemaRefToJson(param.SchemaType, param.Description, param.SubType));
                        if (pi < paramInfos.Length - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }

                    sb.AppendLine("            },");
                    var requiredParams = paramInfos.Where(p => p.Required).Select(p => p.Name).ToArray();
                    if (requiredParams.Length > 0)
                    {
                        sb.Append("            \"required\": [");
                        sb.Append(string.Join(", ", requiredParams.Select(p => $"\"{EscapeJsonString(p)}\"")));
                        sb.AppendLine("]");
                    }
                    else
                    {
                        sb.AppendLine("            \"required\": []");
                    }

                    sb.AppendLine("          }");
                    //sb.AppendLine("          \"unevaluatedProperties\": false");
                    sb.Append("        }");
                    if (ri < matchingOps.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
                sb.AppendLine("      ]");
            }
            sb.AppendLine("    },");

            // Now also generate the array version of the typed expression (e.g. ComponentExpr<Body>Array)
            sb.AppendLine($"    \"{EscapeJsonString(subtype)}ExprArray\": {{");
            sb.AppendLine($"      \"description\": \"An array of typed expressions for {EscapeJsonString(subtype)}.\",");
            sb.AppendLine("      \"oneOf\": [");

            foreach (var arrayMetaOp in arrayMetaOps.Concat(metaOps))
            {
                WriteMetaOp(sb, subtype + "Expr", arrayMetaOp);
            }

            sb.AppendLine("        {");
            sb.AppendLine("          \"type\": \"object\",");
            sb.AppendLine("          \"properties\": {");
            sb.AppendLine("            \"op\": {");
            var arrayOps = matchingArrayOps.SelectMany(g => g).SelectMany(r => r.OpNames).Distinct().ToArray();
            sb.Append("              \"type\": \"string\", \"enum\": [");
            sb.Append(string.Join(", ", arrayOps.Select(n => $"\"{EscapeJsonString(n)}\"")));
            sb.AppendLine("] }");
            sb.AppendLine("          }");
            sb.AppendLine("        }");
            if (matchingArrayOps.Count > 0)
            {
                sb.AppendLine("      ],");
                sb.AppendLine("      \"allOf\": [");
                for (int ri = 0; ri < matchingArrayOps.Count; ri++)
                {
                    var group = matchingArrayOps[ri];
                    var allOpNames = group.SelectMany(r => r.OpNames).Distinct().ToArray();
                    var paramInfos = group.First().Params;

                    sb.AppendLine("        {");
                    sb.AppendLine("          \"if\": {");
                    sb.AppendLine("            \"properties\": {");
                    sb.Append("              \"op\": { \"enum\": [");
                    sb.Append(string.Join(", ", allOpNames.Select(n => $"\"{EscapeJsonString(n)}\"")));
                    sb.AppendLine("] } ");
                    sb.AppendLine("            }");
                    sb.AppendLine("          },");
                    sb.AppendLine("          \"then\": {");
                    sb.AppendLine("            \"properties\": {");

                    for (int pi = 0; pi < paramInfos.Length; pi++)
                    {
                        var param = paramInfos[pi];
                        sb.Append("              \"").Append(EscapeJsonString(param.Name)).Append("\": ");
                        sb.Append(SchemaRefToJson(param.SchemaType, param.Description, param.SubType));
                        if (pi < paramInfos.Length - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }

                    sb.AppendLine("            },");
                    var requiredParams = paramInfos.Where(p => p.Required).Select(p => p.Name).ToArray();
                    if (requiredParams.Length > 0)
                    {
                        sb.Append("            \"required\": [");
                        sb.Append(string.Join(", ", requiredParams.Select(p => $"\"{EscapeJsonString(p)}\"")));
                        sb.AppendLine("]");
                    }
                    else
                    {
                        sb.AppendLine("            \"required\": []");
                    }

                    sb.AppendLine("          }");
                    //sb.AppendLine("          \"unevaluatedProperties\": false");
                    sb.Append("        }");
                    if (ri < matchingArrayOps.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
                sb.AppendLine("      ]");
            }
            else
                sb.AppendLine("      ]");
            sb.AppendLine("    },");
            
        }
        // AnyExpr: union over all expr-based defs makes this auto-update with new component+typed expr defs.
        var distinctAnyExprDefs = anyExprDefs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var distinctAnyExprArrayDefs = anyExprArrayDefs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        sb.AppendLine("    \"AnyExpr\": {");
        sb.AppendLine("      \"description\": \"Any expression type (all generated Expr defs).\",");
        sb.AppendLine("      \"oneOf\": [");
        for (int i = 0; i < distinctAnyExprDefs.Count; i++)
        {
            var exprDef = distinctAnyExprDefs[i];
            sb.Append("        { \"$ref\": \"#/$defs/").Append(exprDef).Append("\" }");
            if (i < distinctAnyExprDefs.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
        sb.AppendLine("      ]");
        sb.AppendLine("    },");
        sb.AppendLine("    \"AnyExprArray\": {");
        sb.AppendLine("      \"description\": \"An array of any expression type (all generated ExprArray defs).\",");
        sb.AppendLine("      \"oneOf\": [");
        for (int i = 0; i < distinctAnyExprArrayDefs.Count; i++)
        {
            var exprArrayDef = distinctAnyExprArrayDefs[i];
            sb.Append("        { \"$ref\": \"#/$defs/").Append(exprArrayDef).Append("\" }");
            if (i < distinctAnyExprArrayDefs.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
        sb.AppendLine("      ]");
        sb.AppendLine("    },");
        sb.AppendLine("    \"BaseExpr\": {");
        sb.AppendLine("      \"description\": \"Anything that can be compiled as an expression.\",");
        sb.AppendLine("      \"oneOf\": [");
        sb.AppendLine("        { \"$ref\": \"#/$defs/AnyExpr\" },");
        sb.AppendLine("        { \"$ref\": \"#/$defs/AnyExprArray\" },");
        sb.AppendLine("        { \"type\": \"array\", \"items\": { \"$ref\": \"#/$defs/BaseExpr\" }, \"description\": \"An array of expressions.\" }");
        sb.AppendLine("      ]");
        sb.AppendLine("    },");
        // Enums
        var enumTypes = registrations
            .SelectMany(r => r.Params)
            .Select(p => p.SchemaType)
            .Where(t => t is INamedTypeSymbol && t.TypeKind == TypeKind.Enum)
            .Distinct(SymbolEqualityComparer.Default)
            .ToList();
        foreach (var enumType in enumTypes)
        {
            if (enumType == null)
                continue;
            var namedType = (INamedTypeSymbol)enumType;

            sb.AppendLine($"    \"{EscapeJsonString(namedType.Name)}Expr\": {{");
            sb.AppendLine($"      \"description\": \"Expression for enum {EscapeJsonString(namedType.Name)}.\",");
            sb.AppendLine("      \"oneOf\": [");
            sb.AppendLine("        {");
            sb.AppendLine("          \"$ref\": \"#/$defs/StringExpr\"");
            sb.AppendLine("        },");
            sb.AppendLine("        {");
            sb.AppendLine("          \"type\": \"string\",");
            sb.Append("          \"enum\": [");
            var enumMembers = namedType.GetMembers().Where(m => m.Kind == SymbolKind.Field).ToList();
            sb.Append(string.Join(", ", enumMembers.Select(m => $"\"{EscapeJsonString(m.Name)}\"")));
            sb.AppendLine("] }");
            sb.AppendLine("      ]");
            sb.AppendLine("    },");
        }
        // Static defs shared across schemas
        sb.AppendLine("    \"ArgumentType\": {");
        sb.AppendLine("      \"type\": \"string\",");
        sb.AppendLine("      \"enum\": [\"entity\", \"position\", \"bodypart\", \"item\", \"boolean\"],");
        sb.AppendLine("      \"description\": \"A skill argument type identifier.\"");
        sb.AppendLine("    }");

        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static IReadOnlyDictionary<string, string> typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"InjuryModel", "body-model.schema.json#/$defs/LayerInjuryModel"},
        {"Entity", "#/$defs/EntityExpr"},
        {"Component", "#/$defs/ComponentExpr"},
        {"EffectExpr", "#/$defs/EffectExpr"},
        {"BaseExpr", "#/$defs/BaseExpr" },
    };
    private static string ArrayRefToJson(ITypeSymbol itemType, string descPart)
    {
        switch (itemType.SpecialType)
        {
            case SpecialType.System_String:
                return $"{{ \"$ref\": \"#/$defs/StringExprArray\"{descPart} }}";
            case SpecialType.System_Single:
                return $"{{ \"$ref\": \"#/$defs/NumberExprArray\"{descPart} }}";
            case SpecialType.System_Boolean:
                return $"{{ \"$ref\": \"#/$defs/ConditionExprArray\"{descPart} }}";
            case SpecialType.System_Object:
                return $"{{ \"$ref\": \"#/$defs/AnyExprArray\"{descPart} }}";
        }
        if (typeMap.TryGetValue(itemType.Name, out string? refValue))
        {
            // Convert e.g. "#/$defs/ComponentExpr" to "#/$defs/ComponentExprArray"
            if (refValue.StartsWith("#/$defs/", StringComparison.OrdinalIgnoreCase) && refValue.EndsWith("Expr", StringComparison.OrdinalIgnoreCase))
            {
                var arrayRefValue = refValue.Substring(0, refValue.Length - "Expr".Length) + "ExprArray";
                return $"{{ \"$ref\": \"{arrayRefValue}\"{descPart} }}";
            }
            return $"{{ \"$ref\": \"{refValue}\"{descPart} }}";
        }

        return $"{{ \"$ref\": \"#/$defs/{EscapeJsonString(itemType.Name)}ExprArray\"{descPart} }}";
    }
    private static string SchemaRefToJson(ITypeSymbol schemaType, string description, string? subtype = null)
    {
        string descPart = description != null
            ? $", \"description\": \"{EscapeJsonString(description)}\""
            : "";

        // Handle array types (e.g. bool[], float[], List<T>)
        if (schemaType is IArrayTypeSymbol arrayType)
        {
            var itemType = arrayType.ElementType;
            // Point to {ItemExpr}Array schema ref generated
            return ArrayRefToJson(itemType, descPart);
        }

        switch (schemaType.SpecialType)
        {
            case SpecialType.System_String:
                if (subtype != null)
                {
                    // This is usually an Enum
                    return $"{{ \"$ref\": \"#/$defs/{EscapeJsonString(subtype)}Expr\"{descPart} }}";
                }
                return $"{{ \"$ref\": \"#/$defs/StringExpr\"{descPart} }}";
            case SpecialType.System_Single:
                return $"{{ \"$ref\": \"#/$defs/NumberExpr\"{descPart} }}";
            case SpecialType.System_Boolean:
                return $"{{ \"$ref\": \"#/$defs/ConditionExpr\"{descPart} }}";
            case SpecialType.System_Object:
                return $"{{ \"$ref\": \"#/$defs/AnyExpr\"{descPart} }}";
        }
        if (schemaType.TypeKind == TypeKind.Enum)
        {
            // Enums are inlined as $defs with their name (e.g. #/$defs/BodyPart), so reference that directly
            return $"{{ \"$ref\": \"#/$defs/{EscapeJsonString(schemaType.Name)}Expr\"{descPart} }}";
        }

        if (schemaType is INamedTypeSymbol namedType)
        {
            if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
            {
                var itemType = namedType.TypeArguments[0];
                switch (namedType.Name)
                {
                    case "IEnumerable":
                    case "List":
                    case "IReadOnlyList":
                    case "ArrayExpr":
                        return ArrayRefToJson(itemType, descPart);
                    case "Tag":
                        return $"{{ \"$ref\": \"_tags.schema.json#/$defs/{itemType.Name}Tag\"{descPart} }}";
                }
            }
            Stack<INamedTypeSymbol> toCheck = new Stack<INamedTypeSymbol>();
            toCheck.Push(namedType);
            while (toCheck.Count > 0)
            {
                var currentType = toCheck.Pop();
                if (currentType.Name == "Component" && subtype != null)
                {
                    return $"{{ \"$ref\": \"#/$defs/{EscapeJsonString(subtype)}Expr\"{descPart} }}";
                }
                // First check for exact type match in the map
                if (typeMap.TryGetValue(currentType.Name, out string? refValue))
                {
                    return $"{{ \"$ref\": \"{refValue}\"{descPart} }}";
                }

                // No match found, check interfaces and base types. Important to check the base type first before interfaces, since e.g. BodyPart implements IEnumerable but should be treated as a ComponentExpr, not an array.
                foreach (var iFace in currentType.AllInterfaces)
                    toCheck.Push(iFace);
                if (currentType.BaseType != null)
                    toCheck.Push(currentType.BaseType);
            }
            // Fallback: use the type name as the schema ref (e.g. for enums or other simple types that we want to inline as $defs)
            var typeName = namedType.Name.Contains('.') ? namedType.Name.Substring(namedType.Name.LastIndexOf('.') + 1) : namedType.Name;
            return $"{{ \"$ref\": \"#/$defs/{EscapeJsonString(typeName)}\"{descPart} }}";
        }

        throw new NotSupportedException($"Unsupported schema type: {schemaType.ToDisplayString()}");
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
        public ITypeSymbol SchemaType { get; }
        public bool Required { get; }
        public string Description { get; }
        public string? SubType { get; }

        public ExprParamInfo(string name, ITypeSymbol schemaType, bool required, string description, string? subType)
        {
            Name = name;
            SchemaType = schemaType;
            Required = required;
            Description = description;
            SubType = subType;
        }
    }

    private class ExprOpRegistration
    {
        public int Category { get; }
        public string[] OpNames { get; }
        public bool IsArray { get; }
        public string FullyQualifiedType { get; }
        public string MethodName { get; }
        public ExprParamInfo[] Params { get; }
        public string? GenericType { get; }

        public ExprOpRegistration(int category, string[] opNames, string fullyQualifiedType, string methodName, ExprParamInfo[] paramInfos, bool isArray, string? genericType)
        {
            Category = category;
            OpNames = opNames;
            FullyQualifiedType = fullyQualifiedType;
            MethodName = methodName;
            Params = paramInfos;
            GenericType = genericType;
            IsArray = isArray;
        }
    }
}
