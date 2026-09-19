using System.Collections;
using System.Reflection;
using System.Text;
using Rpg;
using Rpg.Scripting;

// Regression harness for the expression system.
//
// Loads the whole data corpus through the Compendium exactly as the server does, then walks every
// loaded entry and dumps each compiled expression tree in a stable textual form. The result is a
// golden snapshot: refactors of the compiler must not change it (except where a change is intended
// and reviewed), and any entry that fails to load shows up as an error line.
//
// Usage: ExprCheck <server-root-containing-Data> <snapshot-output-path>

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ExprCheck <server-root> <snapshot-out>");
    return 2;
}

var serverRoot = Path.GetFullPath(args[0]);
var snapshotPath = Path.GetFullPath(args[1]);

if (!Directory.Exists(Path.Combine(serverRoot, "Data")))
{
    Console.Error.WriteLine($"No Data/ directory under {serverRoot}");
    return 2;
}

// Compendium.GetFiles resolves "Data/<folder>" relative to the working directory.
Directory.SetCurrentDirectory(serverRoot);

var diagnostics = new List<string>();
var logger = new Logger("ExprCheck", maxLogs: int.MaxValue);
logger.OnLogAdded += msg =>
{
    if (msg.Level is LogLevel.Error or LogLevel.Warning)
        diagnostics.Add($"{msg.Level}: {Normalize(msg.Message)}");
};

// Stack traces in diagnostics carry absolute paths and line numbers, which churn on every edit and
// would swamp a snapshot diff. Strip them so the diff shows only real behaviour changes.
static string Normalize(string message) =>
    System.Text.RegularExpressions.Regex.Replace(message, @" in .*?:line \d+", "");
Logger.Default = logger;

Compendium.RegisterDefaults();
foreach (var folder in Compendium.Folders)
{
    foreach (var (fName, obj) in Compendium.GetFiles(folder))
        Compendium.RegisterEntry(folder, fName, obj);
}

// The data corpus exercises only a handful of the meta-ops, so these pin the rest: each must
// compile to the stated expression type. They are the regression net for overload resolution.
var smokeTests = new (string Source, Type Expect)[]
{
    ("= 1 + 2 * 3",                                              typeof(Expr<float>)),
    ("= if(target.hp > 0, 1, 2)",                                typeof(Expr<float>)),
    ("= if(target.hp > 0, 'a', 'b')",                            typeof(Expr<string>)),
    ("= concat(['a', 'b'])",                                     typeof(Expr<string>)),
    ("= array_concat([1, 2], [3])",                              typeof(ArrayExpr<float>)),
    ("= map([1, 2, 3], $0 * 2)",                                 typeof(ArrayExpr<float>)),
    ("= sum(map([1, 2, 3], $0 * 2))",                            typeof(Expr<float>)),
    ("= filter([1, 2, 3], $0 > 1)",                              typeof(ArrayExpr<float>)),
    ("= len(filter([1, 2, 3], $0 > 1))",                         typeof(Expr<float>)),
    ("= subarray([1, 2, 3], 1, 2)",                              typeof(ArrayExpr<float>)),
    ("= order([3, 1, 2], $0 < $1)",                              typeof(ArrayExpr<float>)),
    ("= top([1, 2, 3], $0, 2)",                                  typeof(ArrayExpr<float>)),
    ("= bottom([1, 2, 3], $0, 2)",                               typeof(ArrayExpr<float>)),
    ("= with_vars([5], $0 + 1)",                                 typeof(Expr<float>)),
    ("= all([1, 2], $0 > 0)",                                    typeof(Expr<bool>)),
    ("= any([1, 2], $0 > 1)",                                    typeof(Expr<bool>)),
    ("= rand(0.5)",                                              typeof(Expr<bool>)),
    ("= rand(1, 6)",                                             typeof(Expr<float>)),
    ("= len([1, 2, 3])",                                         typeof(Expr<float>)),
    // switch, including the range-case form that replaced the JSON-only switch_range
    ("= switch 2 { 1 => 10, 2 => 20, _ => 0 }",                  typeof(Expr<float>)),
    ("= switch 7 { 0..5 => 'low', 6..10 => 'high', _ => '?' }",  typeof(Expr<string>)),
    ("= switch -1 { -5..0 => 1, _ => 2 }",                       typeof(Expr<float>)),
    // an array of effects must stay unevaluated until something runs it
    ("= composite([nop(), nop()])",                              typeof(EffectExpr)),
    ("= foreach([1, 2], nop())",                                 typeof(EffectExpr)),
};

var smokeFailures = new List<string>();
foreach (var (source, expected) in smokeTests)
{
    try
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(source);
        BaseExpr compiled = expected == typeof(Expr<float>) ? ExpressionCompiler.Compile<float>(json)
            : expected == typeof(Expr<string>) ? ExpressionCompiler.Compile<string>(json)
            : expected == typeof(Expr<bool>) ? ExpressionCompiler.Compile<bool>(json)
            : expected == typeof(EffectExpr) ? ExpressionCompiler.CompileEffect(json)
            : expected == typeof(ArrayExpr<float>) ? ExpressionCompiler.CompileArray<float>(json)
            : throw new Exception($"no compile entry point for {expected}");

        if (!expected.IsInstanceOfType(compiled))
            smokeFailures.Add($"{source}  ->  {compiled.GetType().Name}, expected {expected.Name}");
    }
    catch (Exception e)
    {
        smokeFailures.Add($"{source}  ->  {Normalize(e.Message).Split('\n')[0]}");
    }
}

// Compile-time failures in DSL source must report where they are — the point of the typed AST.
// Each case states the substring the message has to contain and the 1-based column the caret should
// land on. Columns are relative to the DSL source, which begins just after the '=' marker, so they
// line up with the snippet the error prints.
var diagnosticTests = new (string Source, string Contains, int Column)[]
{
    // An unknown op is reported where it is written, not at the enclosing call that failed to
    // resolve around it — no other overload of `sum` could have made it work.
    ("= 1 + notAnOp(2)",            "Unknown operation 'notanop'",  6),
    ("= sum([1, 2]) + nope",        "unknown identifier 'nope'",   16),
    // Overload rejections are reported at the call, with each candidate's reason in the message.
    ("= lerp(1, 2)",                "missing required parameter",   2),
    ("= bp_health('not a part')",   "No overload of 'bp_health'",   2),
    // Syntax errors were already positioned; they still are.
    ("= 1 +",                       "unexpected ''",                5),
    ("= concat(1, 2",               "expected ')'",                13),
};

var diagnosticFailures = new List<string>();
foreach (var (source, contains, column) in diagnosticTests)
{
    try
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(source);
        ExpressionCompiler.Compile<float>(json);
        diagnosticFailures.Add($"{source}  ->  compiled, but should have failed");
    }
    catch (Rpg.Scripting.Dsl.DslException e)
    {
        if (!e.RawMessage.Contains(contains, StringComparison.OrdinalIgnoreCase))
            diagnosticFailures.Add($"{source}  ->  message was \"{e.RawMessage.Split('\n')[0]}\", expected it to mention \"{contains}\"");
        else if (e.Column != column)
            diagnosticFailures.Add($"{source}  ->  reported column {e.Column}, expected {column}");
    }
    catch (Exception e)
    {
        diagnosticFailures.Add($"{source}  ->  {e.GetType().Name} without a source position: {e.Message.Split('\n')[0]}");
    }
}

// The other half of the front end: a field that is a plain JSON value rather than DSL source. What
// such a value means depends on the type the field expects, and that mapping is the one thing the
// data corpus exercises only patchily — most of it has migrated to "= …" strings.
var literalTests = new (string Json, Type Expect, string Describe)[]
{
    ("3.5",        typeof(Expr<float>),  "a number is a constant"),
    ("\"2:5\"",    typeof(Expr<float>),  "a range string still rolls"),
    ("\"1d6\"",    typeof(Expr<float>),  "dice notation still rolls"),
    ("0.25",       typeof(Expr<bool>),   "a number in condition position is a probability"),
    ("\"30%\"",    typeof(Expr<bool>),   "a percentage string is a probability"),
    ("true",       typeof(Expr<bool>),   "a boolean is a constant"),
    ("null",       typeof(Expr<bool>),   "null is false"),
    ("false",      typeof(EffectExpr),   "false in effect position does nothing"),
    ("null",       typeof(EffectExpr),   "null in effect position does nothing"),
    ("\"hello\"",  typeof(Expr<string>), "a string is a literal"),
    ("[1, 2, 3]",  typeof(ArrayExpr<float>), "an array is an array literal"),
};

var literalFailures = new List<string>();
foreach (var (text, expected, describe) in literalTests)
{
    try
    {
        var json = System.Text.Json.JsonDocument.Parse(text).RootElement;
        BaseExpr compiled = expected == typeof(Expr<float>) ? ExpressionCompiler.Compile<float>(json)
            : expected == typeof(Expr<string>) ? ExpressionCompiler.Compile<string>(json)
            : expected == typeof(Expr<bool>) ? ExpressionCompiler.Compile<bool>(json)
            : expected == typeof(EffectExpr) ? ExpressionCompiler.CompileEffect(json)
            : expected == typeof(ArrayExpr<float>) ? ExpressionCompiler.CompileArray<float>(json)
            : throw new Exception($"no compile entry point for {expected}");

        if (!expected.IsInstanceOfType(compiled))
            literalFailures.Add($"{text} ({describe})  ->  {compiled.GetType().Name}, expected {expected.Name}");
    }
    catch (Exception e)
    {
        literalFailures.Add($"{text} ({describe})  ->  {Normalize(e.Message).Split('\n')[0]}");
    }
}

// The legacy op-object encoding is gone. A field still written that way has to say so clearly
// rather than being read as some other kind of value.
var rejectionTests = new (string Json, string Contains)[]
{
    ("{\"op\": \"sum\", \"numbers\": [1, 2]}", "sum(numbers)"),
    ("\"$0\"",                                 "not a variable"),
};

foreach (var (text, contains) in rejectionTests)
{
    try
    {
        ExpressionCompiler.Compile<float>(System.Text.Json.JsonDocument.Parse(text).RootElement);
        literalFailures.Add($"{text}  ->  compiled, but the legacy form should be refused");
    }
    catch (Exception e) when (e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase))
    {
        // expected
    }
    catch (Exception e)
    {
        literalFailures.Add($"{text}  ->  message was \"{e.Message.Split('\n')[0]}\", expected it to mention \"{contains}\"");
    }
}

var sb = new StringBuilder();
sb.AppendLine("# Expression snapshot");
sb.AppendLine("# Stable dump of every compiled expression reachable from a compendium entry.");
sb.AppendLine();

var exprCount = 0;
foreach (var folder in Compendium.Folders.OrderBy(f => f, StringComparer.Ordinal))
{
    var names = Compendium.GetEntryNames(folder).OrderBy(n => n, StringComparer.Ordinal).ToList();
    sb.AppendLine($"== {folder} ({names.Count} entries) ==");

    foreach (var name in names)
    {
        var loaded = Compendium.GetEntries(folder)
            .FirstOrDefault(o => Compendium.GetEntryName(o) == name);
        if (loaded == null)
        {
            sb.AppendLine($"  {name}: <not loaded>");
            continue;
        }

        var found = new List<(string Path, BaseExpr Expr)>();
        ExprWalker.Collect(loaded, name, found);
        foreach (var (path, expr) in found.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            exprCount++;
            sb.AppendLine($"  {path}:");
            sb.Append(ExprDumper.Dump(expr, indent: 4));
        }
    }
    sb.AppendLine();
}

sb.AppendLine($"# total expressions: {exprCount}");
sb.AppendLine();
sb.AppendLine("== smoke tests ==");
sb.AppendLine(smokeFailures.Count == 0
    ? $"all {smokeTests.Length} passed"
    : string.Join("\n", smokeFailures.Select(f => "FAIL " + f)));
sb.AppendLine();
sb.AppendLine("== literal forms ==");
sb.AppendLine(literalFailures.Count == 0
    ? $"all {literalTests.Length + rejectionTests.Length} passed"
    : string.Join("\n", literalFailures.Select(f => "FAIL " + f)));
sb.AppendLine();
sb.AppendLine("== error positions ==");
sb.AppendLine(diagnosticFailures.Count == 0
    ? $"all {diagnosticTests.Length} positioned correctly"
    : string.Join("\n", diagnosticFailures.Select(f => "FAIL " + f)));
sb.AppendLine();
sb.AppendLine("== diagnostics ==");
foreach (var d in diagnostics)
    sb.AppendLine(d);

Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
File.WriteAllText(snapshotPath, sb.ToString().ReplaceLineEndings("\n"));

var errorCount = diagnostics.Count(d => d.StartsWith("Error"));
Console.WriteLine($"Snapshot written to {snapshotPath}");
Console.WriteLine($"  expressions: {exprCount}");
Console.WriteLine($"  warnings:    {diagnostics.Count - errorCount}");
Console.WriteLine($"  errors:      {errorCount}");
Console.WriteLine($"  smoke:       {smokeTests.Length - smokeFailures.Count}/{smokeTests.Length} passed");
foreach (var failure in smokeFailures)
    Console.WriteLine($"    FAIL {failure}");
Console.WriteLine($"  literals:    {literalTests.Length + rejectionTests.Length - literalFailures.Count}/{literalTests.Length + rejectionTests.Length} passed");
foreach (var failure in literalFailures)
    Console.WriteLine($"    FAIL {failure}");
Console.WriteLine($"  diagnostics: {diagnosticTests.Length - diagnosticFailures.Count}/{diagnosticTests.Length} positioned");
foreach (var failure in diagnosticFailures)
    Console.WriteLine($"    FAIL {failure}");
return errorCount == 0 && smokeFailures.Count == 0
       && literalFailures.Count == 0 && diagnosticFailures.Count == 0 ? 0 : 1;

/// <summary>
/// Walks a loaded compendium entry's object graph and collects every <see cref="BaseExpr"/> it can
/// reach, labelled by the member path that led to it.
/// </summary>
static class ExprWalker
{
    public static void Collect(object root, string rootLabel, List<(string, BaseExpr)> into)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, rootLabel, into, seen, depth: 0);
    }

    private static void Walk(object? node, string path, List<(string, BaseExpr)> into,
                             HashSet<object> seen, int depth)
    {
        if (node == null || depth > 12) return;
        if (node is string || node.GetType().IsPrimitive || node is Enum) return;
        if (!seen.Add(node)) return;

        if (node is BaseExpr expr)
        {
            into.Add((path, expr));
            return; // the dumper renders the subtree
        }

        if (node is IDictionary dict)
        {
            foreach (DictionaryEntry kv in dict)
                Walk(kv.Value, $"{path}[{kv.Key}]", into, seen, depth + 1);
            return;
        }

        if (node is IEnumerable seq)
        {
            var i = 0;
            foreach (var item in seq)
                Walk(item, $"{path}[{i++}]", into, seen, depth + 1);
            return;
        }

        var type = node.GetType();
        // Only descend through the game's own model types, never into framework/BCL graphs.
        if (type.Assembly != typeof(BaseExpr).Assembly) return;

        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                              .OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            object? value;
            try { value = f.GetValue(node); } catch { continue; }
            Walk(value, $"{path}.{f.Name}", into, seen, depth + 1);
        }
    }
}

/// <summary>
/// Renders an expression tree as indented text. Field order is sorted so the output is stable
/// across runs and across refactors that reorder declarations.
/// </summary>
static class ExprDumper
{
    public static string Dump(BaseExpr expr, int indent)
    {
        var sb = new StringBuilder();
        Write(expr, sb, indent, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return sb.ToString();
    }

    private static void Write(object? node, StringBuilder sb, int indent, HashSet<object> seen)
    {
        var pad = new string(' ', indent);

        switch (node)
        {
            case null:
                sb.AppendLine($"{pad}null");
                return;
            case string s:
                sb.AppendLine($"{pad}\"{s}\"");
                return;
            case Enum e:
                sb.AppendLine($"{pad}{e.GetType().Name}.{e}");
                return;
            case float or double or int or long or bool or byte or short or uint or ulong or ushort:
                sb.AppendLine($"{pad}{Convert.ToString(node, System.Globalization.CultureInfo.InvariantCulture)}");
                return;
        }

        if (!seen.Add(node!))
        {
            sb.AppendLine($"{pad}<cycle>");
            return;
        }

        if (node is BaseExpr expr)
        {
            sb.AppendLine($"{pad}{TypeName(expr.GetType())}");
            var type = expr.GetType();
            var members = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                              .Where(f => !f.Name.Contains('<')) // skip auto-property backing fields' duplicates
                              .OrderBy(f => f.Name, StringComparer.Ordinal);
            foreach (var f in members)
            {
                object? value;
                try { value = f.GetValue(expr); } catch { continue; }
                sb.AppendLine($"{pad}  {f.Name}:");
                Write(value, sb, indent + 4, seen);
            }

            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                  .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (p.DeclaringType == typeof(object)) continue;
                object? value;
                try { value = p.GetValue(expr); } catch { continue; }
                if (value is not (BaseExpr or IEnumerable) || value is string) continue;
                sb.AppendLine($"{pad}  {p.Name}:");
                Write(value, sb, indent + 4, seen);
            }
            return;
        }

        if (node is IDictionary dict)
        {
            sb.AppendLine($"{pad}{{{dict.Count}}}");
            foreach (var key in dict.Keys.Cast<object>().OrderBy(k => k?.ToString(), StringComparer.Ordinal))
            {
                sb.AppendLine($"{pad}  {key}:");
                Write(dict[key], sb, indent + 4, seen);
            }
            return;
        }

        if (node is IEnumerable seq)
        {
            var items = seq.Cast<object?>().ToList();
            sb.AppendLine($"{pad}[{items.Count}]");
            foreach (var item in items)
                Write(item, sb, indent + 2, seen);
            return;
        }

        sb.AppendLine($"{pad}<{TypeName(node!.GetType())}>");
    }

    private static string TypeName(Type t)
    {
        if (!t.IsGenericType) return t.Name;
        var name = t.Name[..t.Name.IndexOf('`')];
        return $"{name}<{string.Join(",", t.GetGenericArguments().Select(TypeName))}>";
    }
}
