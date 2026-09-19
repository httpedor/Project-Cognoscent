using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExprGenerator;

/// <summary>
/// Emits the op table from op declarations.
/// <para>
/// An op is a static method marked <c>[ExprOp("name", …)]</c> whose parameters are real, typed C#
/// parameters. Everything the rest of the system needs about that op — its parameter names, their
/// expression types, which are optional, its result type, and the code to construct it — is derived
/// from that one signature here. Ops therefore never restate their parameters in attributes and
/// never parse <c>JsonElement</c> themselves.
/// </para>
/// <para>
/// This generator emits <em>data only</em>: a list of <c>OpDef</c> registrations. Resolution,
/// overload selection and diagnostics are hand-written in <c>Rpg/Scripting/Types</c>, where they can
/// be read and debugged, rather than being generated control flow.
/// </para>
/// </summary>
[Generator]
public sealed class OpTableGenerator : IIncrementalGenerator
{
    private const string ExprOpAttributeMetadataName = "Rpg.Scripting.ExprOpAttribute";
    private const string DocAttributeMetadataName = "Rpg.Scripting.DocAttribute";

    private static readonly DiagnosticDescriptor MustBeStatic = new(
        id: "EXPR010",
        title: "Op factory must be static",
        messageFormat: "Method '{0}' marked with [ExprOp] must be static",
        category: "OpTableGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedParameter = new(
        id: "EXPR011",
        title: "Unsupported op parameter type",
        messageFormat: "Parameter '{0}' of op '{1}' has type '{2}', which is not an expression type (expected Expr<T>, ArrayExpr<T> or EffectExpr)",
        category: "OpTableGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedReturn = new(
        id: "EXPR012",
        title: "Unsupported op return type",
        messageFormat: "Op '{0}' returns '{1}', which is not an expression type (expected Expr<T>, ArrayExpr<T> or EffectExpr)",
        category: "OpTableGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ops = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ExprOpAttributeMetadataName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => Describe(ctx))
            .Where(static op => op is not null)
            .Select(static (op, _) => op!);

        context.RegisterSourceOutput(ops.Collect(), Emit);
    }

    // ── model ───────────────────────────────────────────────────────────────

    private sealed class OpModel
    {
        public string[] Names = System.Array.Empty<string>();
        public string? Description;
        public string DeclaringType = "";
        public string MethodName = "";
        public string ResultType = "";               // ExprType construction expression
        public List<ParamModel> Params = new();
        public List<Diagnostic> Diagnostics = new();
    }

    private sealed class ParamModel
    {
        public string Name = "";
        public string TypeExpr = "";                 // ExprType construction expression
        /// <summary>C# source that turns <c>a[i]</c> into the declared parameter type.</summary>
        public string Accessor = "";
        public bool Required;
        public string? Description;
    }

    private static OpModel? Describe(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;

        var model = new OpModel
        {
            DeclaringType = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MethodName = method.Name
        };

        var names = new List<string>();
        foreach (var attr in ctx.Attributes)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array)
                foreach (var item in attr.ConstructorArguments[0].Values)
                    if (item.Value is string s)
                        names.Add(s);

            foreach (var named in attr.NamedArguments)
                if (named.Key == "Description" && named.Value.Value is string d)
                    model.Description = d;
        }

        if (names.Count == 0) return null;
        model.Names = names.ToArray();

        if (!method.IsStatic)
        {
            model.Diagnostics.Add(Diagnostic.Create(MustBeStatic, method.Locations.FirstOrDefault(), method.ToDisplayString()));
            return model;
        }

        var result = MapType(method.ReturnType);
        if (result == null)
        {
            model.Diagnostics.Add(Diagnostic.Create(
                UnsupportedReturn, method.Locations.FirstOrDefault(), names[0], method.ReturnType.ToDisplayString()));
            return model;
        }
        model.ResultType = result;

        foreach (var p in method.Parameters)
        {
            var required = !p.HasExplicitDefaultValue;
            var index = model.Params.Count;

            var mapped = MapType(p.Type);
            string accessor;

            if (mapped != null)
            {
                // An expression-typed parameter: hand the compiled expression straight through.
                var cast = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                accessor = $"({cast})a[{index}]{(required ? "!" : "")}";
            }
            else
            {
                // A literal-typed parameter (string / number / bool / enum): unwrap the constant.
                mapped = MapLiteralType(p.Type);
                if (mapped == null)
                {
                    model.Diagnostics.Add(Diagnostic.Create(
                        UnsupportedParameter, p.Locations.FirstOrDefault(), p.Name, names[0], p.Type.ToDisplayString()));
                    return model;
                }
                accessor = LiteralAccessor(p.Type, index, required);
            }

            string? doc = null;
            foreach (var attr in p.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != DocAttributeMetadataName) continue;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string t)
                    doc = t;
            }

            model.Params.Add(new ParamModel
            {
                Name = TrimAt(p.Name),
                TypeExpr = mapped,
                Accessor = accessor,
                Required = required,
                Description = doc
            });
        }

        return model;
    }

    /// <summary>The <c>ExprType</c> a bare (non-<c>Expr&lt;&gt;</c>) parameter accepts at a call site.</summary>
    private static string? MapLiteralType(ITypeSymbol type)
    {
        var bare = type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;
        if (bare is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            bare = nullable.TypeArguments[0];

        // A variable reference may be written as an index or as a context name, so the call site
        // accepts either and the literal helper sorts it out.
        if (bare.ToDisplayString() == "Rpg.Scripting.Types.VariableRef")
            return "global::Rpg.Scripting.Types.ExprType.Unknown";

        if (bare.TypeKind == TypeKind.Enum)
            return "global::Rpg.Scripting.Types.ExprType.String";

        switch (bare.SpecialType)
        {
            case SpecialType.System_String:
                return "global::Rpg.Scripting.Types.ExprType.String";
            case SpecialType.System_Boolean:
                return "global::Rpg.Scripting.Types.ExprType.Bool";
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
                return "global::Rpg.Scripting.Types.ExprType.Number";
        }
        return null;
    }

    /// <summary>C# source that unwraps a literal argument into the declared parameter type.</summary>
    private static string LiteralAccessor(ITypeSymbol type, int index, bool required)
    {
        var bare = type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;
        var nullableValue = bare is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n;
        if (nullableValue) bare = ((INamedTypeSymbol)bare).TypeArguments[0];

        var optional = !required;
        var arg = optional ? $"a[{index}]" : $"a[{index}]!";
        const string L = "global::Rpg.Scripting.Types.Literals";

        if (bare.ToDisplayString() == "Rpg.Scripting.Types.VariableRef")
            return optional ? $"{L}.VariableRefOrNull({arg})" : $"{L}.VariableRef({arg})";

        if (bare.TypeKind == TypeKind.Enum)
        {
            var enumName = bare.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return optional ? $"{L}.EnumOrNull<{enumName}>({arg})" : $"{L}.Enum<{enumName}>({arg})";
        }

        return bare.SpecialType switch
        {
            SpecialType.System_String => optional ? $"{L}.StringOrNull({arg})" : $"{L}.String({arg})",
            SpecialType.System_Boolean => optional ? $"{L}.BoolOrNull({arg})" : $"{L}.Bool({arg})",
            SpecialType.System_Int32 => optional ? $"(int?){L}.NumberOrNull({arg})" : $"(int){L}.Number({arg})",
            SpecialType.System_UInt32 => optional ? $"(uint?){L}.NumberOrNull({arg})" : $"(uint){L}.Number({arg})",
            SpecialType.System_Int64 => optional ? $"(long?){L}.NumberOrNull({arg})" : $"(long){L}.Number({arg})",
            SpecialType.System_Double => optional ? $"(double?){L}.NumberOrNull({arg})" : $"(double){L}.Number({arg})",
            _ => optional ? $"{L}.NumberOrNull({arg})" : $"{L}.Number({arg})"
        };
    }

    /// <summary>C# lets a parameter be named <c>@default</c>; the op name is the unescaped form.</summary>
    private static string TrimAt(string name) => name.StartsWith("@") ? name.Substring(1) : name;

    /// <summary>
    /// Translates the declared CLR type of a parameter or return value into the C# source of an
    /// <c>ExprType</c>. Returns null when the type is not an expression type.
    /// </summary>
    private static string? MapType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated && type is INamedTypeSymbol { IsValueType: false })
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        // Ops routinely return a concrete expression class (RangeNumberExpr, LogEffect, …) rather
        // than Expr<T> itself, so walk the inheritance chain to find the expression base.
        for (var current = type; current != null; current = current.BaseType)
        {
            var display = current.OriginalDefinition.ToDisplayString();

            if (display == "Rpg.Scripting.EffectExpr")
                return "global::Rpg.Scripting.Types.ExprType.Void";

            // A lambda parameter is a function type. Generated ops take single-parameter lambdas
            // (`all`, `any`, `foreach` all bind one element); anything needing a higher arity is
            // registered by hand in MetaOps, where the signature is written out in full.
            if (display == "Rpg.Scripting.LambdaExpr<T>"
                && current is INamedTypeSymbol lambda && lambda.TypeArguments.Length == 1)
            {
                var produced = MapValueType(lambda.TypeArguments[0]);
                return produced == null
                    ? null
                    : $"global::Rpg.Scripting.Types.ExprType.Func({produced}, global::Rpg.Scripting.Types.ExprType.Unknown)";
            }

            if (current is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
            {
                var inner = named.TypeArguments[0];
                if (display == "Rpg.Scripting.ArrayExpr<T>")
                {
                    var element = MapValueType(inner);
                    return element == null ? null : $"global::Rpg.Scripting.Types.ExprType.ArrayOf({element})";
                }
                if (display == "Rpg.Scripting.Expr<T>")
                {
                    // ArrayExpr<T> derives from Expr<IEnumerable<T>>; MapValueType turns that back
                    // into an array type, so both spellings agree.
                    return MapValueType(inner);
                }
            }
        }

        return null;
    }

    /// <summary>Translates the T of an <c>Expr&lt;T&gt;</c> into <c>ExprType</c> source.</summary>
    private static string? MapValueType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

        switch (type.SpecialType)
        {
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return "global::Rpg.Scripting.Types.ExprType.Number";
            case SpecialType.System_Boolean:
                return "global::Rpg.Scripting.Types.ExprType.Bool";
            case SpecialType.System_String:
                return "global::Rpg.Scripting.Types.ExprType.String";
            case SpecialType.System_Object:
                return "global::Rpg.Scripting.Types.ExprType.Unknown";
        }

        var display = type.OriginalDefinition.ToDisplayString();

        // Both spellings of "an effect" are the void type. ArrayExpr<EffectExpr> reaches here with
        // T = EffectExpr and used to fall through to Of(typeof(EffectExpr)), so a parameter declared
        // as an array of effects wanted a different type from the one an effect actually has, and
        // nothing could ever be passed to it.
        if (display is "Rpg.Scripting.NoReturn" or "Rpg.Scripting.EffectExpr")
            return "global::Rpg.Scripting.Types.ExprType.Void";

        // IEnumerable<T> / List<T> in the T position means a nested array.
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1 &&
            (display == "System.Collections.Generic.IEnumerable<T>" ||
             display == "System.Collections.Generic.List<T>" ||
             display == "System.Collections.Generic.IReadOnlyList<T>"))
        {
            var element = MapValueType(named.TypeArguments[0]);
            return element == null ? null : $"global::Rpg.Scripting.Types.ExprType.ArrayOf({element})";
        }

        var qualified = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"global::Rpg.Scripting.Types.ExprType.Of(typeof({qualified}))";
    }

    // ── emission ────────────────────────────────────────────────────────────

    private static void Emit(SourceProductionContext context, ImmutableArray<OpModel> models)
    {
        var registrations = new StringBuilder();

        foreach (var model in models.OrderBy(m => m.Names.Length > 0 ? m.Names[0] : "", System.StringComparer.Ordinal))
        {
            foreach (var diagnostic in model.Diagnostics)
                context.ReportDiagnostic(diagnostic);

            if (model.Diagnostics.Count > 0 || model.Names.Length == 0)
                continue;

            registrations.Append(RenderOp(model));
        }

        if (registrations.Length == 0)
            registrations.Append("        // no signature-declared ops found\n");

        var source = $$"""
            // <auto-generated />
            #nullable enable

            using System;
            using System.Collections.Immutable;
            using Rpg.Scripting.Types;

            namespace Rpg.Scripting;

            /// <summary>
            /// Registers every signature-declared op into <see cref="OpRegistry"/>.
            /// Generated from the [ExprOp] method signatures; do not edit.
            /// </summary>
            public static class GeneratedOps
            {
                private static bool _registered;

                public static void RegisterAll()
                {
                    if (_registered) return;
                    _registered = true;

            {{registrations.ToString().TrimEnd()}}
                }
            }
            """;

        context.AddSource("GeneratedOps.g.cs", source);
    }

    private static string RenderOp(OpModel model)
    {
        var aliases = string.Join(", ", model.Names.Select(n => $"\"{Escape(n)}\""));

        var paramList = model.Params.Count == 0
            ? "ImmutableArray<OpParam>.Empty"
            : "ImmutableArray.Create(\n" + string.Join(",\n", model.Params.Select(p =>
                  $"                    new OpParam(\"{Escape(p.Name)}\", {p.TypeExpr}, {(p.Required ? "true" : "false")}, {Literal(p.Description)})")) + ")";

        var args = string.Join(",\n", model.Params.Select(p => $"                    {p.Accessor}"));

        // `_` is the inferred result type: only polymorphic ops (registered by hand in MetaOps)
        // need it, since a generated op's result type is fixed by its return type.
        var call = model.Params.Count == 0
            ? $"{model.DeclaringType}.{model.MethodName}()"
            : $"{model.DeclaringType}.{model.MethodName}(\n{args})";

        return $$"""
                    OpRegistry.Register(new OpDef
                    {
                        Name = "{{Escape(model.Names[0])}}",
                        Aliases = ImmutableArray.Create({{aliases}}),
                        Params = {{paramList}},
                        Result = {{model.ResultType}},
                        Description = {{Literal(model.Description)}},
                        DeclaringMethod = "{{Escape(model.DeclaringType + "." + model.MethodName)}}",
                        Factory = (a, _) => {{call}}
                    });

            """;
    }

    private static string Literal(string? value) => value == null ? "null" : $"\"{Escape(value)}\"";

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}
