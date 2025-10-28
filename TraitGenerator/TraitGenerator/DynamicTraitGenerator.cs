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
    private static readonly DiagnosticDescriptor TargetOverloadCollision = new(
        id: "TG003",
        title: "Target class already has method with same signature",
        messageFormat: "Type '{0}' already has a method that would collide with mixin method '{1}' from '{2}'.",
        category: "TraitGenerator",
        DiagnosticSeverity.Info,
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

                // Avoid applying a mixin to itself
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

        // For each target, generate a partial that directly injects mixin members (no composition)
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

            // Collect usings from all mixin source files
            var usingSet = new HashSet<string>();
            foreach (var mixin in mixins)
            {
                foreach (var declRef in mixin.DeclaringSyntaxReferences)
                {
                    var node = declRef.SyntaxTree.GetRoot();
                    foreach (var u in node.DescendantNodes().OfType<UsingDirectiveSyntax>())
                    {
                        var text = u.ToFullString().Trim();
                        if (!string.IsNullOrWhiteSpace(text)) usingSet.Add(text);
                    }
                }
            }

            var ns = target.ContainingNamespace is { IsGlobalNamespace: false }
                ? target.ContainingNamespace.ToDisplayString()
                : null;

            var sb = new StringBuilder();

            // Emit collected usings first so copied member syntax binds properly
            foreach (var u in usingSet)
            {
                sb.AppendLine(u);
            }
            if (usingSet.Count > 0) sb.AppendLine();

            if (ns is not null)
            {
                sb.AppendLine($"namespace {ns};");
                sb.AppendLine();
            }

            // Emit nested containers if target is nested
            EmitContainingTypesPreamble(sb, target);

            // Emit the actual target partial type header
            EmitPartialTypeHeader(sb, target);

            // Build a quick lookup of existing members on the target to avoid collisions
            var existingMemberIndex = BuildExistingMemberIndex(target);

            foreach (var mixin in mixins)
            {
                // Copy instance members directly into the target
                foreach (var declRef in mixin.DeclaringSyntaxReferences)
                {
                    if (declRef.GetSyntax() is not TypeDeclarationSyntax mixinDecl)
                        continue;

                    var tree = declRef.SyntaxTree;
                    var sm = context.Compilation.GetSemanticModel(tree);

                    foreach (var member in mixinDecl.Members)
                    {
                        switch (member)
                        {
                            case FieldDeclarationSyntax f:
                            {
                                // Skip static or const (avoid global duplication/static conflicts)
                                var isStatic = f.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                                var isConst = f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
                                if (isStatic || isConst) break;

                                // Check collisions per variable name
                                bool anyAdded = false;
                                foreach (var v in f.Declaration.Variables)
                                {
                                    var name = v.Identifier.Text;
                                    if (existingMemberIndex.Fields.Contains(name))
                                        continue;
                                    existingMemberIndex.Fields.Add(name);
                                    anyAdded = true;
                                }
                                if (!anyAdded) break;
                                // Append original field syntax
                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            case PropertyDeclarationSyntax p:
                            {
                                // Skip static properties
                                if (p.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) break;
                                // Skip abstract properties — they act as requirements on the target
                                if (p.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))) break;

                                var name = p.Identifier.Text;
                                if (existingMemberIndex.Properties.Contains(name)) break;
                                existingMemberIndex.Properties.Add(name);

                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            case MethodDeclarationSyntax m:
                            {
                                // Skip static, accessors, operators, no-body partials
                                if (m.Modifiers.Any(mm => mm.IsKind(SyntaxKind.StaticKeyword))) break;
                                // Skip abstract methods — requirements only
                                if (m.Modifiers.Any(mm => mm.IsKind(SyntaxKind.AbstractKeyword))) break;
                                if (m.Body is null && m.ExpressionBody is null) break; // interface-like or partial w/o body

                                var sym = sm.GetDeclaredSymbol(m) as IMethodSymbol;
                                if (sym is null) break;
                                if (sym.MethodKind != MethodKind.Ordinary) break;

                                var sigKey = MakeMethodSignatureKey(sym);
                                if (existingMemberIndex.MethodSignatures.Contains(sigKey))
                                {
                                    context.ReportDiagnostic(Diagnostic.Create(
                                        TargetOverloadCollision,
                                        target.Locations.FirstOrDefault(),
                                        target.ToDisplayString(),
                                        sym.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                                        mixin.ToDisplayString()));
                                    break;
                                }
                                existingMemberIndex.MethodSignatures.Add(sigKey);

                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            case EventDeclarationSyntax eDecl:
                            {
                                if (eDecl.Modifiers.Any(mm => mm.IsKind(SyntaxKind.StaticKeyword))) break;
                                // Skip abstract events — requirements only
                                if (eDecl.Modifiers.Any(mm => mm.IsKind(SyntaxKind.AbstractKeyword))) break;
                                var name = eDecl.Identifier.Text;
                                if (existingMemberIndex.Events.Contains(name)) break;
                                existingMemberIndex.Events.Add(name);
                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            case EventFieldDeclarationSyntax eField:
                            {
                                if (eField.Modifiers.Any(mm => mm.IsKind(SyntaxKind.StaticKeyword))) break;
                                // Event field declarations cannot be abstract in C#, but guard just in case
                                if (eField.Modifiers.Any(mm => mm.IsKind(SyntaxKind.AbstractKeyword))) break;
                                bool anyAdded = false;
                                foreach (var v in eField.Declaration.Variables)
                                {
                                    var name = v.Identifier.Text;
                                    if (existingMemberIndex.Events.Contains(name)) continue;
                                    existingMemberIndex.Events.Add(name);
                                    anyAdded = true;
                                }
                                if (!anyAdded) break;
                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            case TypeDeclarationSyntax nestedType:
                            {
                                // Copy nested types (non-static) to support helper types used by mixin
                                if (nestedType.Modifiers.Any(mm => mm.IsKind(SyntaxKind.StaticKeyword))) break;
                                var name = nestedType.Identifier.Text;
                                if (existingMemberIndex.NestedTypes.Contains(name)) break;
                                existingMemberIndex.NestedTypes.Add(name);
                                sb.AppendLine(Indent(member.ToFullString(), 1));
                                sb.AppendLine();
                                break;
                            }
                            default:
                                break;
                        }
                    }
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

    private static string Indent(string text, int depth)
    {
        var indent = new string(' ', depth * 4);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                sb.AppendLine();
            }
            else
            {
                sb.Append(indent);
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    private sealed class MemberIndex
    {
        public HashSet<string> Fields { get; } = new();
        public HashSet<string> Properties { get; } = new();
        public HashSet<string> Events { get; } = new();
        public HashSet<string> NestedTypes { get; } = new();
        public HashSet<string> MethodSignatures { get; } = new();
    }

    private static MemberIndex BuildExistingMemberIndex(INamedTypeSymbol target)
    {
        var idx = new MemberIndex();
        foreach (var m in target.GetMembers())
        {
            switch (m)
            {
                case IFieldSymbol f:
                    idx.Fields.Add(f.Name);
                    break;
                case IPropertySymbol p:
                    idx.Properties.Add(p.Name);
                    break;
                case IMethodSymbol mm when mm.MethodKind == MethodKind.Ordinary:
                    idx.MethodSignatures.Add(MakeMethodSignatureKey(mm));
                    break;
                case IEventSymbol e:
                    idx.Events.Add(e.Name);
                    break;
                case INamedTypeSymbol nt when nt.TypeKind == TypeKind.Class || nt.TypeKind == TypeKind.Struct || nt.TypeKind == TypeKind.Interface || nt.TypeKind == TypeKind.Enum || nt.TypeKind == TypeKind.Delegate:
                    idx.NestedTypes.Add(nt.Name);
                    break;
            }
        }
        return idx;
    }

    private static string MakeMethodSignatureKey(IMethodSymbol method)
    {
        var sb = new StringBuilder();
        sb.Append(method.Name);
        sb.Append('`').Append(method.Arity);
        sb.Append('(');
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var p = method.Parameters[i];
            sb.Append(p.RefKind.ToString());
            sb.Append(':');
            sb.Append(p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (i < method.Parameters.Length - 1) sb.Append(',');
        }
        sb.Append(')');
        return sb.ToString();
    }
}