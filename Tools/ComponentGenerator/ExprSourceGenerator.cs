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

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
