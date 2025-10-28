using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraitGenerator;

[Generator]
public class DynamicTraitGenerator : ISourceGenerator
{
    private static readonly DiagnosticDescriptor TargetNotPartial = new(
        id: "TG001",
        title: "Target class must be partial",
        messageFormat: "Type '{0}' must be declared partial to receive mixin members from '{1}'.",
        category: "TraitGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MixinNeedsParameterlessCtor = new(
        id: "TG002",
        title: "Mixin requires parameterless constructor",
        messageFormat: "Mixin type '{0}' applied to '{1}' must have a parameterless constructor to be instantiated.",
        category: "TraitGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
    }


    public void Execute(GeneratorExecutionContext context)
    {
        // Collect all class symbols defined in the compilation once (for interface expansion)
        var allClassSymbols = new List<INamedTypeSymbol>();
        var typeDecls = new List<(SyntaxTree tree, TypeDeclarationSyntax typeDecl)>();

        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var t in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                typeDecls.Add((tree, t));
            }
        }

        foreach (var (tree, t) in typeDecls)
        {
            var sm = context.Compilation.GetSemanticModel(tree);
            if (sm.GetDeclaredSymbol(t) is INamedTypeSymbol { TypeKind: TypeKind.Class } sym)
            {
                allClassSymbols.Add(sym);
            }
        }

        // Map: target class -> set of mixin types that should be applied
        var targetToMixins = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        // Find all types that have [Mixin(typeof(TargetType))] applied; these are the mixin providers
        foreach (var (tree, t) in typeDecls)
        {
            var sm = context.Compilation.GetSemanticModel(tree);
            var typeSymbol = sm.GetDeclaredSymbol(t) as INamedTypeSymbol;
            if (typeSymbol is null)
                continue;

            var mixinAttrs = typeSymbol.GetAttributes()
                .Where(a => a.AttributeClass?.Name == "MixinAttribute")
                .ToArray();

            if (mixinAttrs.Length == 0)
                continue;

            foreach (var attr in mixinAttrs)
            {
                if (attr.ConstructorArguments.Length == 0)
                    continue;

                var targetArg = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                if (targetArg is null)
                    continue;

                // Determine the set of concrete class targets
                IEnumerable<INamedTypeSymbol> concreteTargets = Enumerable.Empty<INamedTypeSymbol>();
                if (targetArg.TypeKind == TypeKind.Interface)
                {
                    // All classes in the compilation that implement this interface
                    concreteTargets = allClassSymbols.Where(c => ImplementsInterface(c, targetArg));
                }
                else if (targetArg.TypeKind == TypeKind.Class)
                {
                    concreteTargets = new[] { targetArg };
                }
                else
                {
                    // Unsupported target kind; skip
                    continue;
                }

                // Avoid applying a mixin to itself (prevents self-recursive constructor and stack overflow)
                concreteTargets = concreteTargets.Where(c => !SymbolEqualityComparer.Default.Equals(c, typeSymbol));

                foreach (var target in concreteTargets)
                {
                    // Skip if target is from metadata (not declared in this compilation)
                    if (target.Locations.All(l => l.IsInMetadata))
                        continue;

                    if (!targetToMixins.TryGetValue(target, out var set))
                    {
                        set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                        targetToMixins[target] = set;
                    }
                    set.Add(typeSymbol);
                }
            }
        }

        // For each target, generate a partial with delegating properties for each mixin
        foreach (var kvp in targetToMixins)
        {
            var target = kvp.Key;
            var mixins = kvp.Value;

            if (!IsPartial(target))
            {
                foreach (var mixin in mixins)
                {
                    context.ReportDiagnostic(Diagnostic.Create(TargetNotPartial,
                        target.Locations.FirstOrDefault(),
                        target.ToDisplayString(),
                        mixin.ToDisplayString()));
                }
                // Don't generate for non-partial targets to avoid compile errors
                continue;
            }

            var ns = target.ContainingNamespace is { IsGlobalNamespace: false }
                ? target.ContainingNamespace.ToDisplayString()
                : null;

            var sb = new StringBuilder();
            if (ns is not null)
            {
                sb.AppendLine($"namespace {ns};");
                sb.AppendLine();
            }

            // Emit nested containers if target is nested
            EmitContainingTypesPreamble(sb, target);

            // Emit the actual target partial type header
            EmitPartialTypeHeader(sb, target);

            // Emit one private field per mixin and delegating properties
            foreach (var mixin in mixins)
            {
                // Ensure mixin is instantiable (non-abstract class with parameterless ctor)
                if (mixin.TypeKind != TypeKind.Class || mixin.IsAbstract || !HasParameterlessCtor(mixin))
                {
                    context.ReportDiagnostic(Diagnostic.Create(MixinNeedsParameterlessCtor,
                        mixin.Locations.FirstOrDefault(),
                        mixin.ToDisplayString(),
                        target.ToDisplayString()));
                    continue;
                }

                var mixinFieldName = MakeStableFieldName(mixin);
                var mixinTypeName = mixin.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"    private readonly {mixinTypeName} {mixinFieldName} = new {mixinTypeName}();");
                sb.AppendLine();

                // Delegate instance, non-static, accessible properties
                foreach (var member in mixin.GetMembers().OfType<IPropertySymbol>())
                {
                    if (member.IsStatic)
                        continue;

                    // Only delegate public/internal to avoid accessibility issues
                    if (!(member.DeclaredAccessibility == Accessibility.Public || member.DeclaredAccessibility == Accessibility.Internal))
                        continue;

                    // Skip indexers
                    if (member.IsIndexer)
                        continue;

                    // Avoid name collisions with existing members on target
                    if (target.GetMembers(member.Name).Any())
                        continue;

                    var propTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var accessibility = AccessibilityToString(member.DeclaredAccessibility);
                    var getter = member.GetMethod != null ? $"get => {mixinFieldName}.{member.Name};" : null;
                    var setter = member.SetMethod != null ? $"set => {mixinFieldName}.{member.Name} = value;" : null;

                    sb.Append($"    {accessibility} {propTypeName} {member.Name} {{ ");
                    if (getter != null) sb.Append(getter + " ");
                    if (setter != null) sb.Append(setter + " ");
                    sb.AppendLine("}");
                    sb.AppendLine();
                }

                // Delegate instance, non-static, accessible ordinary methods
                foreach (var method in mixin.GetMembers().OfType<IMethodSymbol>())
                {
                    if (method.IsStatic) continue;
                    if (method.MethodKind != MethodKind.Ordinary) continue; // skip constructors/accessors/operators/etc.

                    // Only delegate public/internal to avoid accessibility issues
                    if (!(method.DeclaredAccessibility == Accessibility.Public || method.DeclaredAccessibility == Accessibility.Internal))
                        continue;

                    // Build a signature collision check against the target
                    bool SignatureCollidesWithTarget()
                    {
                        foreach (var existing in target.GetMembers(method.Name).OfType<IMethodSymbol>())
                        {
                            if (existing.MethodKind != MethodKind.Ordinary) continue;
                            if (existing.TypeParameters.Length != method.TypeParameters.Length) continue;
                            if (existing.Parameters.Length != method.Parameters.Length) continue;

                            bool allParamsMatch = true;
                            for (int i = 0; i < method.Parameters.Length; i++)
                            {
                                var mP = method.Parameters[i];
                                var eP = existing.Parameters[i];
                                if (mP.RefKind != eP.RefKind) { allParamsMatch = false; break; }
                                if (!SymbolEqualityComparer.Default.Equals(mP.Type, eP.Type)) { allParamsMatch = false; break; }
                            }
                            if (!allParamsMatch) continue;

                            // Return type does not participate in overload resolution, but a same-signature method would collide
                            return true;
                        }
                        return false;
                    }

                    if (SignatureCollidesWithTarget())
                        continue;

                    var accessibility = AccessibilityToString(method.DeclaredAccessibility);
                    var retType = method.ReturnsVoid
                        ? "void"
                        : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Type parameters (for generic methods)
                    string MethodTypeParams()
                    {
                        if (method.TypeParameters.Length == 0) return string.Empty;
                        var names = string.Join(", ", method.TypeParameters.Select(tp => tp.Name));
                        return $"<{names}>";
                    }

                    // Constraints (where clauses)
                    string MethodConstraints()
                    {
                        if (method.TypeParameters.Length == 0) return string.Empty;
                        var pieces = new List<string>();
                        foreach (var tp in method.TypeParameters)
                        {
                            var constraints = new List<string>();
                            if (tp.HasReferenceTypeConstraint) constraints.Add("class");
                            if (tp.HasValueTypeConstraint) constraints.Add("struct");
                            constraints.AddRange(tp.ConstraintTypes.Select(c => c.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                            if (tp.HasConstructorConstraint) constraints.Add("new()");
                            if (constraints.Count > 0)
                                pieces.Add($" where {tp.Name} : {string.Join(", ", constraints)}");
                        }
                        return string.Concat(pieces);
                    }

                    string ParamDecl(IParameterSymbol p)
                    {
                        var mod = p.RefKind switch
                        {
                            RefKind.Ref => "ref ",
                            RefKind.Out => "out ",
                            RefKind.In => "in ",
                            _ => string.Empty
                        };
                        var type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        return $"{mod}{type} {p.Name}";
                    }

                    string ArgPass(IParameterSymbol p)
                    {
                        var mod = p.RefKind switch
                        {
                            RefKind.Ref => "ref ",
                            RefKind.Out => "out ",
                            RefKind.In => "in ",
                            _ => string.Empty
                        };
                        return $"{mod}{p.Name}";
                    }

                    var paramDecls = string.Join(", ", method.Parameters.Select(ParamDecl));
                    var argPasses = string.Join(", ", method.Parameters.Select(ArgPass));
                    var typeParams = MethodTypeParams();
                    var constraints = MethodConstraints();

                    sb.AppendLine($"    {accessibility} {retType} {method.Name}{typeParams}({paramDecls}){constraints}");
                    sb.AppendLine("    {");
                    if (method.ReturnsVoid)
                    {
                        sb.AppendLine($"        {mixinFieldName}.{method.Name}{typeParams}({argPasses});");
                    }
                    else
                    {
                        sb.AppendLine($"        return {mixinFieldName}.{method.Name}{typeParams}({argPasses});");
                    }
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }

            // Close target partial and any containing types
            sb.AppendLine("}"); // close target
            CloseContainingTypes(sb, target);

            var hintName = MakeHintName(target);
            context.AddSource(hintName, sb.ToString());
        }
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol iface)
    {
        // Match both exact and generic original definitions, and include transitive interfaces (AllInterfaces covers base classes and base interfaces)
        foreach (var implemented in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, iface))
                return true;
            if (implemented.IsGenericType && iface.IsGenericType)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, iface.OriginalDefinition))
                    return true;
            }
            else if (implemented.IsGenericType && !iface.IsGenericType)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, iface))
                    return true;
            }
            else if (!implemented.IsGenericType && iface.IsGenericType)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented, iface.OriginalDefinition))
                    return true;
            }
        }
        return false;
    }

    private static bool IsPartial(INamedTypeSymbol type)
    {
        foreach (var declRef in type.DeclaringSyntaxReferences)
        {
            if (declRef.GetSyntax() is TypeDeclarationSyntax t)
            {
                if (t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    return true;
            }
        }
        return false;
    }

    private static bool HasParameterlessCtor(INamedTypeSymbol type)
    {
        // If no constructors declared, implicit parameterless exists
        if (!type.InstanceConstructors.Any())
            return true;
        return type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility != Accessibility.Private);
    }

    private static string MakeStableFieldName(INamedTypeSymbol mixin)
    {
        // Create a field name that's unlikely to collide, based on fully-qualified metadata name
        var name = mixin.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('.', '_').Replace('+', '_').Replace('<', '_').Replace('>', '_');
        return $"_mixin_{name}";
    }

    private static string MakeHintName(INamedTypeSymbol target)
    {
        var name = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('.', '_').Replace('+', '_');
        return $"{name}.Mixins.g.cs";
    }

    private static void EmitContainingTypesPreamble(StringBuilder sb, INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        var cur = type.ContainingType;
        while (cur != null)
        {
            containingTypes.Push(cur);
            cur = cur.ContainingType;
        }
        while (containingTypes.Count > 0)
        {
            var t = containingTypes.Pop();
            var kind = t.IsRecord
                ? (t.TypeKind == TypeKind.Struct ? "record struct" : "record")
                : t.TypeKind switch
                {
                    TypeKind.Struct => "struct",
                    TypeKind.Interface => "interface",
                    _ => "class"
                };
            var accessibility = AccessibilityToString(t.DeclaredAccessibility);
            var typeParams = TypeParamsList(t);
            sb.AppendLine($"{accessibility} partial {kind} {t.Name}{typeParams}");
            sb.AppendLine("{");
        }
    }

    private static void CloseContainingTypes(StringBuilder sb, INamedTypeSymbol type)
    {
        var depth = 0;
        var cur = type.ContainingType;
        while (cur != null)
        {
            depth++;
            cur = cur.ContainingType;
        }
        for (int i = 0; i < depth; i++) sb.AppendLine("}");
    }

    private static void EmitPartialTypeHeader(StringBuilder sb, INamedTypeSymbol type)
    {
        var kind = type.IsRecord
            ? (type.TypeKind == TypeKind.Struct ? "record struct" : "record")
            : type.TypeKind switch
            {
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                _ => "class"
            };
        var accessibility = AccessibilityToString(type.DeclaredAccessibility);
        var typeParams = TypeParamsList(type);
        sb.AppendLine($"{accessibility} partial {kind} {type.Name}{typeParams}");
        sb.AppendLine("{");
    }

    private static string TypeParamsList(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0) return string.Empty;
        var names = string.Join(", ", type.TypeParameters.Select(tp => tp.Name));
        return $"<{names}>";
    }

    private static string AccessibilityToString(Accessibility a)
    {
        return a switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "public"
        };
    }
}