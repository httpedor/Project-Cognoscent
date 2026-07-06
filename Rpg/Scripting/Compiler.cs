using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Health;
using Rpg.Features;
using Rpg.Health;

namespace Rpg.Scripting;
public sealed class EvalContext
{
    /// <summary>
    /// Array of variable values.
    /// </summary>
    public object[] Variables = Array.Empty<object>();
    /// <summary>
    /// The board on which the script is being executed, if any.
    /// </summary>
    public Board? Board = null;
    /// <summary>
    /// The entity executing the script, if any.
    /// </summary>
    public Entity? Caller = null;
    /// <summary>
    /// The target entity of the script, if any.
    /// </summary>
    public Entity? Target = null;
    /// <summary>
    /// The exact bodypart of the target entity being affected, if any.
    /// </summary>
    public Component? TargetComponent = null;

    public EvalContext()
    {
        Variables = Array.Empty<object>();
    }
    public EvalContext(params object[] args)
    {
        Variables = args;
    }

    public object? GetVariable(int index)
    {
        switch (index)
        {
            case -1:
                return Target;
            case -2:
                return Caller;
            case -3:
                return TargetComponent;
        }

        if (index < 0 || index >= Variables.Length)
            throw new IndexOutOfRangeException($"Variable index {index} is out of range.");
        return Variables[index];
    }

    public object? GetVariable(string symbolName)
    {
        int symbolId = ExpressionCompiler.GetVariableSymbolId(symbolName);
        return GetVariable(symbolId);
    }

    public T Eval<T>(Expr<T> expr)
    {
        return expr.Eval(this);
    }

    public EvalContext WithTarget(Entity? entity, Entity? caller = null, Component? targetComponent = null)
    {
        if (caller == null)
            caller = this.Caller;
        if (targetComponent == null)
            targetComponent = this.TargetComponent;
        return new EvalContext
        {
            Variables = this.Variables,
            Board = this.Board,
            Caller = caller,
            Target = entity,
            TargetComponent = targetComponent
        };
    }
    public EvalContext WithVariables(params object[] variables)
    {
        return new EvalContext
        {
            Variables = variables,
            Board = this.Board,
            Caller = this.Caller,
            Target = this.Target,
            TargetComponent = this.TargetComponent
        };
    }

    public void ShiftVariables(int shift)
    {
        if (shift == 0)
            return;
        if (shift < 0)
        {
            // Shift left: remove the first -shift variables
            Variables = Variables.Skip(-shift).ToArray();
        }
        else
        {
            // Shift right: add shift null variables at the beginning
            Variables = new object[shift].Concat(Variables).ToArray();
        }
    }
    public EvalContext WithShiftedVariables(int shift)
    {
        var newContext = WithVariables(Variables);
        newContext.ShiftVariables(shift);
        return newContext;
    }
}

public static class ExpressionCompiler
{
    private static readonly MethodInfo _compileComponentAsMethod = typeof(ExpressionCompiler).GetMethod(nameof(CompileComponentAs), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _compileEnumMethod = typeof(ExpressionCompiler).GetMethod(nameof(CompileEnum), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _compileCompendiumEntryMethod = typeof(ExpressionCompiler).GetMethod(nameof(CompileCompendiumEntry), BindingFlags.NonPublic | BindingFlags.Static)!;

    // ── shared helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="element"/> is DSL source (a string starting with '='), desugar it to its
    /// JSON expression tree; otherwise return it unchanged. Every compile entry point runs this so a
    /// <c>"= …"</c> string is accepted anywhere an expression is expected.
    /// </summary>
    private static JsonElement Desugared(JsonElement element)
        => Dsl.DslCompiler.IsDslSource(element, out var src) ? Dsl.DslCompiler.Desugar(src) : element;

    /// <summary>Recognizes a <c>"$N"</c> variable-reference string and yields its index N.</summary>
    private static bool TryGetVarIndex(JsonElement element, out int index)
    {
        index = -1;
        if (element.ValueKind != JsonValueKind.String) return false;
        var s = element.GetString()!;
        if (s.Length == 0 || s[0] != '$') return false;
        if (int.TryParse(s.AsSpan(1), out index)) return true;
        throw new Exception($"Invalid variable ID: {s[1..]}");
    }

    /// <summary>Reads the required string <c>"op"</c> property, with a clear error if it is missing.</summary>
    private static string RequireOp(JsonElement element, string what)
        => element.TryGetProperty("op", out var op) && op.GetString() is { } s
            ? s
            : throw new Exception($"{what} is missing a string 'op' property.");

    private static Expr<T> Expect<T>(BaseExpr? compiled)
        => compiled as Expr<T> ?? throw new Exception($"Compiled expression is not Expr<{typeof(T).Name}>.");

    private static ArrayExpr<T> ExpectArray<T>(object? compiled)
        => compiled as ArrayExpr<T> ?? throw new Exception($"Compiled expression is not ArrayExpr<{typeof(T).Name}>.");

    /// <summary>Invokes one of the open generic compile methods (component/enum/compendium) closed over T.</summary>
    private static Expr<T> InvokeGeneric<T>(MethodInfo openMethod, JsonElement element)
        => openMethod.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { element }) as Expr<T>
            ?? throw new Exception($"Compiled expression is not Expr<{typeof(T).Name}>.");

    public static int GetVariableSymbolId(string symbolName)
    {
        if (symbolName.StartsWith("$"))
            symbolName = symbolName[1..];
        if (int.TryParse(symbolName, out int symbolId))
            return symbolId;
        switch (symbolName.ToLower())
        {
            case "target":
                return -1;
            case "caller":
                return -2;
            case "target_part":
            case "target_component":
                return -3;
            default:
                throw new Exception($"Unknown variable symbol name: {symbolName}");
        }
    }
    private static Expr<IEnumerable<T>>? CheckForMetaArrays<T>(JsonElement element)
    {
        // Array-producing ops are matched first, before the scalar meta-op check below — otherwise
        // "map" would be caught by CheckForMetaExpr as a non-array MapExpr<IEnumerable<T>> and its
        // cast to ArrayExpr<T> would fail.
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("op", out var opElement) && opElement.ValueKind == JsonValueKind.String)
        {
            switch (opElement.GetString()?.ToLower())
            {
                case "map":
                    return new MapArrayExpr<T>(
                        CompileBaseExprArray(element.GetProperty("values")),
                        Compile<T>(element.GetProperty("expression")));
                case "concat":
                case "append":
                case "join":
                    var arrays = new List<ArrayExpr<T>>();
                    if (element.TryGetProperty("arrays", out var arraysElement) && arraysElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var arrayEl in arraysElement.EnumerateArray())
                        {
                            var compiledArray = CompileArray<T>(arrayEl);
                            arrays.Add(compiledArray);
                        }
                    }
                    return new ArrayAppendExpr<T>(arrays.ToArray());
                case "subarray":
                    var array = CompileArray<T>(element.GetProperty("array"));
                    Expr<float> startIndex;
                    if (element.TryGetProperty("start", out var startElement))
                        startIndex = CompileNumber(startElement);
                    else
                        startIndex = new ConstNumberExpr(0);
                    Expr<float> length;
                    if (element.TryGetProperty("length", out var lengthElement))
                        length = CompileNumber(lengthElement);
                    else
                        length = new ConstNumberExpr(float.MaxValue);
                    return new SubArrayExpr<T>(array, startIndex, length);
                case "filter":
                    {
                        var vals = CompileArray<T>(element.GetProperty("values"));
                        var condition = CompileCondition(element.GetProperty("condition"));
                        return new FilterArrayExpr<T>(vals, condition);
                    }
                case "order":
                case "sort":
                case "order_by":
                    {
                        var vals = CompileArray<T>(element.GetProperty("values"));
                        var comparison = CompileCondition(element.GetProperty("comparison"));
                        return new OrderArrayExpr<T>(vals, comparison);
                    }
                case "max_elements":
                case "max":
                case "top":
                    {
                        var src = CompileArray<T>(element.GetProperty("values"));
                        var valueExpr = CompileNumber(element.GetProperty("value"));
                        Expr<float> countExpr;
                        if (element.TryGetProperty("count", out var countEl))
                            countExpr = CompileNumber(countEl);
                        else
                            countExpr = new ConstNumberExpr(float.MaxValue);
                        return new MaxElementsArrayExpr<T>(src, valueExpr, countExpr);
                    }
                case "min_elements":
                case "min":
                case "bottom":
                    {
                        var src = CompileArray<T>(element.GetProperty("values"));
                        var valueExpr = CompileNumber(element.GetProperty("value"));
                        Expr<float> countExpr;
                        if (element.TryGetProperty("count", out var countEl2))
                            countExpr = CompileNumber(countEl2);
                        else
                            countExpr = new ConstNumberExpr(float.MaxValue);
                        return new MinElementsArrayExpr<T>(src, valueExpr, countExpr);
                    }
                /*case "with_vars":
                case "with_var":
                case "with_variable":
                case "with_variables":
                    var innerArray = CompileArray<T>(element.GetProperty("expression"));
                    var variables = CompileArray<object>(element.GetProperty("variables"));
                    return new ArrayWithVarsExpr<T>(innerArray, variables);*/
            }
        }
        // Not an array-specific op: fall back to scalar meta-ops that also yield arrays
        // (a `$N` variable, or if/switch/call_expr whose branches are arrays).
        return CheckForMetaExpr<IEnumerable<T>>(element);
    }
    private static Expr<T>? CheckForMetaExpr<T>(JsonElement element)
    {
        if (TryGetVarIndex(element, out var varIndex))
            return new VarExpr<T>(varIndex);
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("op", out var opElement) || opElement.ValueKind != JsonValueKind.String)
            return null;


        var opStr = opElement.GetString()?.ToLower();
        switch (opStr)
        {
            case "map":
                var values = CompileBaseExprArray(element.GetProperty("values"));
                var transformation = Compile<T>(element.GetProperty("expression"));
                return new MapExpr<T>(values, transformation);
            case "if":
                var condition = CompileCondition(element.GetProperty("condition"));
                var trueExpr = Compile<T>(element.GetProperty("true"));
                var falseExpr = Compile<T>(element.GetProperty("false"));
                return new ConditionalExpr<T>(condition, trueExpr, falseExpr);
            case "if_chain":
                var branches = new List<(Expr<bool>, Expr<T>)>();
                if (element.TryGetProperty("branches", out var branchesElement) && branchesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var branchEl in branchesElement.EnumerateArray())
                    {
                        if (branchEl.ValueKind != JsonValueKind.Object)
                            throw new Exception($"Invalid branch element in if_chain expression: {branchEl}");
                        if (!branchEl.TryGetProperty("condition", out var branchConditionElement) || !branchEl.TryGetProperty("expr", out var branchExprElement))
                            throw new Exception($"Branch element in if_chain expression missing 'condition' or 'expr' property: {branchEl}");
                        var branchCondition = CompileCondition(branchConditionElement);
                        var branchExpr = Compile<T>(branchExprElement);
                        branches.Add((branchCondition, branchExpr));
                    }
                }
                Expr<T>? ifChainDefault = null;
                if (element.TryGetProperty("default", out var ifChainDefaultElement))
                    ifChainDefault = Compile<T>(ifChainDefaultElement);
                return new IfElseExpr<T>(branches, ifChainDefault);
            case "with_variables":
            case "with_vars":
            case "with_var":
                var variables = CompileArray<object>(element.GetProperty("variables"));
                var innerExpr = Compile<T>(element.GetProperty("expression"));
                return new WithVariablesExpr<T>(variables, innerExpr);
            case "switch_number":
                var numberExpr = CompileNumber(element.GetProperty("value"));
                Dictionary<float, Expr<T>> cases = new();
                if (element.TryGetProperty("cases", out var casesElement) && casesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var caseProp in casesElement.EnumerateObject())
                    {
                        if (float.TryParse(caseProp.Name, out float caseValue))
                        {
                            var caseExpr = Compile<T>(caseProp.Value);
                            cases[caseValue] = caseExpr;
                        }
                        else
                        {
                            throw new Exception($"Invalid case value in switch_number expression: {caseProp.Name}");
                        }
                    }
                }
                Expr<T>? defaultCase = null;
                if (element.TryGetProperty("default", out var defaultElement))
                {
                    defaultCase = Compile<T>(defaultElement);
                }
                return new SwitchNumberExpr<T>(numberExpr, cases, defaultCase);
            case "switch_range":
                var rangeNumberExpr = CompileNumber(element.GetProperty("value"));
                List<(float, float, Expr<T>)> rangeCases = new();
                if (element.TryGetProperty("cases", out var rangeCasesElement) && rangeCasesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var caseEl in rangeCasesElement.EnumerateArray())
                    {
                        if (caseEl.ValueKind != JsonValueKind.Object)
                            throw new Exception($"Invalid case element in switch_range expression: {caseEl}");
                        if (!caseEl.TryGetProperty("min", out var minElement) || !caseEl.TryGetProperty("max", out var maxElement) || !caseEl.TryGetProperty("expr", out var exprElement))
                            throw new Exception($"Case element in switch_range expression missing 'min', 'max' or 'expr' property: {caseEl}");
                        var minValue = JsonHelpers.GetFloat(minElement);
                        var maxValue = JsonHelpers.GetFloat(maxElement);
                        var caseExpr = Compile<T>(exprElement);
                        rangeCases.Add((minValue, maxValue, caseExpr));
                    }
                }
                Expr<T>? rangeDefaultCase = null;
                if (element.TryGetProperty("default", out var rangeDefaultElement))
                {
                    rangeDefaultCase = Compile<T>(rangeDefaultElement);
                }
                return new SwitchRangeExpr<T>(rangeNumberExpr, rangeCases, rangeDefaultCase);
            case "function":
            case "run_function":
            case "call_function":
            case "run_expr":
            case "call_expr":
                if (!element.TryGetProperty("id", out var functionIdEl) || functionIdEl.ValueKind != JsonValueKind.String)
                    throw new Exception("Function expression missing 'id' property or 'id' is not a string.");
                ArrayExpr<object>? argsExpr = null;
                if (element.TryGetProperty("args", out var argsElement))
                    argsExpr = CompileArray<object>(argsElement);
                var functionId = functionIdEl.GetString()!;
                string libraryId;
                if (functionId.Contains(":"))
                {
                    var parts = functionId.Split(':', 2);
                    libraryId = parts[0];
                    var exprName = parts[1];
                    return new CastExpr<T>(new RunExprFromLibraryExpr(libraryId, exprName));
                }
                if (!element.TryGetProperty("library", out var libIdEl) || libIdEl.ValueKind != JsonValueKind.String)
                    throw new Exception("Function expression missing 'library' property or 'library' is not a string.");
                libraryId = libIdEl.GetString()!;

                return new CastExpr<T>(new RunExprFromLibraryExpr(libraryId, functionId, argsExpr));
        }
        return null;
    }
    public static ArrayExpr<T> CompileArray<T>(JsonElement element)
    {
        element = Desugared(element);

        // A `$N` variable in array position must become a VarArrayExpr — handle it before the meta
        // check, which would otherwise return a (non-array) VarExpr<IEnumerable<T>> and fail the cast.
        if (TryGetVarIndex(element, out var varIndex))
            return new VarArrayExpr<T>(varIndex);

        var metaExpr = CheckForMetaArrays<T>(element);
        if (metaExpr != null)
            return (ArrayExpr<T>)metaExpr;

        if (typeof(T) == typeof(object))
            return ExpectArray<T>(CompileUnknownArray(element));

        if (typeof(T).IsAssignableTo(typeof(EffectExpr)))
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new Exception($"Invalid effect array expression element: {element}");
            var effects = element.EnumerateArray().Select(el => CompileEffect(el)).ToArray();
            return ExpectArray<T>(new EffectArray(effects));
        }

        if (element.ValueKind == JsonValueKind.Array)
            return new ConstArrayExpr<T>(element.EnumerateArray().Select(el => Compile<T>(el)).ToList());

        if (element.ValueKind != JsonValueKind.Object)
            throw new Exception($"Invalid array expression element: {element}");

        var op = RequireOp(element, "Array expression");
        if (typeof(T) == typeof(string)) return ExpectArray<T>(ExprRegistry.CompileStringArray(element, op));
        if (typeof(T) == typeof(float)) return ExpectArray<T>(ExprRegistry.CompileNumberArray(element, op));
        if (typeof(T) == typeof(bool)) return ExpectArray<T>(ExprRegistry.CompileConditionArray(element, op));
        if (typeof(Component).IsAssignableFrom(typeof(T)) && ExprRegistry.CompileComponentArray(element, op) is { } comp)
            return ExpectArray<T>(comp);
        if (typeof(T) == typeof(Entity) && ExprRegistry.CompileEntityArray(element, op) is { } ent)
            return ExpectArray<T>(ent);
        throw new Exception($"Unknown array operation: {op}");
    }
    public static Expr<T> Compile<T>(JsonElement element)
    {
        element = Desugared(element);
        var metaExpr = CheckForMetaExpr<T>(element);
        if (metaExpr != null)
            return metaExpr;

        if (typeof(T) == typeof(object))
        {
            // Literal operands (e.g. the right side of `x == 0`) compile directly; CompileUnknown
            // only handles op-objects, and CheckForMetaExpr already handled `$N` variables.
            BaseExpr literal = element.ValueKind switch
            {
                JsonValueKind.Number => new ConstNumberExpr(element.GetSingle()),
                JsonValueKind.True or JsonValueKind.False => new ConstConditionExpr(element.GetBoolean()),
                JsonValueKind.String => new StringLiteralExpr(element.GetString()!),
                _ => CompileUnknown(element),
            };
            return Expect<T>(new CastExpr<object>(literal));
        }
        if (typeof(T) == typeof(NoReturn)) return Expect<T>(CompileEffect(element, false));
        if (typeof(T) == typeof(float)) return Expect<T>(CompileNumber(element));
        if (typeof(T).IsNumericType()) return Expect<T>(new CastExpr<T>(CompileNumber(element)));
        if (typeof(T) == typeof(bool)) return Expect<T>(CompileCondition(element));
        if (typeof(T) == typeof(Entity)) return Expect<T>(CompileEntity(element));
        if (typeof(T) == typeof(string)) return Expect<T>(CompileString(element));
        if (typeof(Component).IsAssignableFrom(typeof(T))) return InvokeGeneric<T>(_compileComponentAsMethod, element);
        if (typeof(T).IsEnum) return InvokeGeneric<T>(_compileEnumMethod, element);
        if (Compendium.IsFolder<T>()) return InvokeGeneric<T>(_compileCompendiumEntryMethod, element);
        throw new Exception($"Unsupported expression type for compilation: {typeof(T)}");
    }
    public static BaseExpr CompileUnknown(JsonElement element)
    {
        element = Desugared(element);
        if (element.ValueKind != JsonValueKind.Object)
            throw new Exception($"Invalid expression element: {element}");
        var op = RequireOp(element, "Expression");
        var builders = ExprRegistry.GetAllBuilders(op.ToLower());
        foreach (var builder in builders)
        {
            if (builder.IsArray)
                continue;
            try
            {
                var expr = builder.Builder(element);
                if (expr != null)
                    return (BaseExpr)expr;
            }
            catch
            {
                // If the builder throws an exception, ignore it and try the next one. This allows us to attempt multiple builders for the same op until we find one that matches the expected parameters.
            }
        }
        throw new Exception("Unknown expression operation: " + op);
    }

    /// <summary>
    /// Tries to compile anything that can be casted to BaseExpr. This differs from Compile<object> in that this will compile both arrayexprs, effects and expr<t>
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static BaseExpr CompileBaseExpr(JsonElement element)
    {
        element = Desugared(element);
        var metaExpr = CheckForMetaArrays<object>(element);
        if (metaExpr != null)
            return metaExpr;
        var expr = CompileUnknown(element);
        if (expr != null)
            return expr;

        var arrExpr = CompileUnknownArray(element);
        if (arrExpr != null)
            return arrExpr;
        throw new Exception("Compiled expression cannot be compiled: " + element);
    }

    /// <summary>
    /// Tries to compile an array of anything that can be casted to BaseExpr. This differs from CompileArray<object> in that the elements of this array will be compiled with CompileBaseExpr, allowing for arrays of effects, arrays of arrays, etc.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static Expr<IEnumerable<object>> CompileBaseExprArray(JsonElement element)
    {
        var arrMetaExpr = CheckForMetaArrays<object>(element);
        if (arrMetaExpr != null)
            return arrMetaExpr;
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<Expr<object>>();
            foreach (var el in element.EnumerateArray())
            {
                list.Add(new CastExpr<object>(CompileBaseExpr(el)));
            }
            return new CastArrayExpr<object>(new ConstArrayExpr<object>(list.ToArray()));
        }
        var arrExpr = CompileUnknownArray(element);
        if (arrExpr != null)
            return arrExpr;
        throw new Exception("Compiled array expression cannot be compiled: " + element);
    }

    private static Expr<IEnumerable<object>> CompileUnknownArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<Expr<object>>();
            foreach (var el in element.EnumerateArray())
            {
                list.Add(new CastExpr<object>(Compile<object>(el)));
            }
            return new CastArrayExpr<object>(new ConstArrayExpr<object>(list.ToArray()));
        }
        if (element.ValueKind != JsonValueKind.Object)
            throw new Exception($"Invalid expression element: {element}");
        var op = RequireOp(element, "Array expression");
        var builders = ExprRegistry.GetAllBuilders(op.ToLower());
        // Prefer a native array builder (e.g. bp_by_tag yields an array directly), then fall back
        // to treating a single-value op as a one-element array. Both wrap into CastArrayExpr<object>.
        foreach (var preferArray in new[] { true, false })
        {
            foreach (var builder in builders)
            {
                if (builder.IsArray != preferArray)
                    continue;
                try
                {
                    var expr = builder.Builder(element);
                    if (expr is BaseExpr baseExpr)
                        return new CastArrayExpr<object>(baseExpr);
                }
                catch
                {
                    // If the builder throws, try the next one — lets us attempt multiple builders
                    // for the same op until one matches the expected parameters.
                }
            }
        }
        throw new Exception("Unknown expression operation: " + op);
    }
    private static Expr<float> CompileNumber(JsonElement element)
    {
        element = Desugared(element);
        // Meta-ops (if / switch / with_vars / call_expr / $vars) are valid in any typed position,
        // including nested ones reached directly (e.g. a filter body), not only via Compile<T>.
        var meta = CheckForMetaExpr<float>(element);
        if (meta != null) return meta;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new ConstNumberExpr(element.GetSingle());

            case JsonValueKind.String:
            {
                string name = element.GetString()!;
                var randDenominators = new char[] {'d', 'D', '-', ':', ','};
                if (name.Length > 2 && name.IndexOfAny(randDenominators) >= 0)
                    return new RandomExpr(name);
                if (float.TryParse(name, out float parsedValue))
                    return new ConstNumberExpr(parsedValue);
                throw new Exception($"Invalid number expression string: {name}");
            }

            case JsonValueKind.Object:
                var op = RequireOp(element, "Number expression");
                return ExprRegistry.CompileNumber(element, op.ToLower())
                    ?? throw new Exception("Unknown number operation: " + op);

            default:
                throw new Exception($"Invalid expression json: {element}");
        }
    }
    public static EffectExpr CompileEffect(JsonElement element, bool withMetaOps = true)
    {
        element = Desugared(element);
        if (withMetaOps)
        {
            var metaEffect = CheckForMetaExpr<NoReturn>(element);
            if (metaEffect != null)
                return new CallExprEffect(metaEffect);
        }
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string name = RequireOp(element, "Effect expression");
                return ExprRegistry.CompileEffect(element, name.ToLower())
                    ?? throw new Exception("Unknown effect: " + name);
            case JsonValueKind.String:
                string effectName = element.GetString()!;
                switch (effectName.ToLower())
                {
                    case "null":
                    case "nop":
                    case "noeffect":
                    case "no_effect":
                        return new NoEffectExpr();
                    default:
                        throw new Exception("Unknown effect: " + effectName);
                }
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return new NoEffectExpr();
            default:
                throw new Exception($"Invalid effect element: {element}");
        }
    }
    private static Expr<bool> CompileCondition(JsonElement element)
    {
        element = Desugared(element);
        // CheckForMetaExpr already handles `$N` variables (as well as if/switch/call_expr).
        var meta = CheckForMetaExpr<bool>(element);
        if (meta != null) return meta;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string op = RequireOp(element, "Condition expression");
                return ExprRegistry.CompileCondition(element, op.ToLower())
                    ?? throw new Exception("Unknown condition operation: " + op);
            case JsonValueKind.String:
                {
                    string name = element.GetString()!;
                    if (name == "true") return new ConstConditionExpr(true);
                    if (name == "false") return new ConstConditionExpr(false);
                    if (name.EndsWith("%"))
                        return float.TryParse(name[..^1], out float pct)
                            ? new RandomConditionExpr(new ConstNumberExpr(pct / 100f))
                            : throw new Exception($"Invalid probability value in condition: {name}");
                    if (name.StartsWith("!$") && int.TryParse(name.AsSpan(2), out int negId))
                        return new NotConditionExpr(new VarExpr<bool>(negId));
                    throw new Exception($"Invalid condition string: {name}");
                }
            case JsonValueKind.Null:
                return new ConstConditionExpr(false);
            case JsonValueKind.Number:
                {
                    float value = element.GetSingle();
                    if (value > 1f)
                        throw new Exception($"Invalid probability value in condition: {value}");
                    if (value < 0f)
                        throw new Exception($"Invalid probability value in condition: {value}");
                    return new RandomConditionExpr(new ConstNumberExpr(value));
                }
            case JsonValueKind.True:
                return new ConstConditionExpr(true);
            case JsonValueKind.False:
                return new ConstConditionExpr(false);
            
            default:
                throw new Exception($"Invalid condition element: {element}");
        }
    }

    private static Expr<Component?> CompileComponent(JsonElement element)
    {
        element = Desugared(element);
        // A variable may hold an entity or component; wrap it and cast where possible.
        if (TryGetVarIndex(element, out var varIndex))
            return new VarExpr<Component?>(varIndex);

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var name = element.GetString()!;
                if (name.Equals("target_component", StringComparison.OrdinalIgnoreCase))
                    return new TargetComponentExpr();
                throw new Exception($"Invalid component expression string: {name}");
            case JsonValueKind.Object:
                string op = RequireOp(element, "Component expression");
                return ExprRegistry.CompileComponent(element, op.ToLower())
                    ?? throw new Exception("Unknown component operation: " + op);
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return new NoComponentExpr();
            default:
                throw new Exception($"Invalid component element: {element}");
        }
    }

    private static Expr<T?> CompileComponentAs<T>(JsonElement element) where T : Component
    {
        if (typeof(T) == typeof(Component))
            return CompileComponent(element) as Expr<T?> ?? throw new Exception("Failed to compile component expression.");
        Expr<Component?> inner;
        try
        {
            inner = CompileComponent(element);
        }
        catch
        {
            // If it fails to compile as a component, try compiling as an entity and then casting.
            var entityExpr = CompileEntity(element);
            inner = new EntityToComponentExpr<T>(entityExpr);
        }
        return new CastExpr<T?>(inner);
    }

    private static Expr<Entity?> CompileEntity(JsonElement element)
    {
        element = Desugared(element);
        if (TryGetVarIndex(element, out var varIndex))
            return new VarExpr<Entity?>(varIndex);

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString()!.ToLowerInvariant() switch
                {
                    "self" or "caller" => new CallerEntityExpr(),
                    "target" => new TargetEntityExpr(),
                    "target_part" or "target_component" => new TargetComponentEntityExpr(),
                    var other => throw new Exception($"Invalid entity selector string: {other}"),
                };
            case JsonValueKind.Object:
                return CompileSelectorObj(element, RequireOp(element, "Selector expression"));
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return new NoEntityExpr();
            default:
                throw new Exception($"Invalid selector element: {element}");
        }
    }
    private static Expr<Entity?> CompileSelectorObj(JsonElement obj, string selectorName)
    {
        var result = ExprRegistry.CompileEntity(obj, selectorName.ToLower());
        if (result != null) return result;
        throw new Exception("Unknown selector: " + selectorName);
    }
    private static Expr<string> CompileString(JsonElement element)
    {
        element = Desugared(element);
        // CheckForMetaExpr already handles `$N` variables (as well as if/switch/call_expr).
        var meta = CheckForMetaExpr<string>(element);
        if (meta != null) return meta;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return new StringLiteralExpr(element.GetString()!);
            case JsonValueKind.Object:
                return CompileStringObj(element, RequireOp(element, "String expression").ToLower());
            default:
                throw new Exception($"Invalid string expression element: {element}");
        }
    }
    private static Expr<string> CompileStringObj(JsonElement obj, string op)
    {
        var result = ExprRegistry.CompileString(obj, op);
        if (result != null) return result;
        throw new Exception("Unknown string operation: " + op);
    }

    private static EnumExpr<T> CompileEnum<T>(JsonElement element) where T : struct, Enum
    {
        Expr<string> valueExpr = CompileString(element);
        return new EnumExpr<T>(valueExpr);
    }

    private static CompendiumEntryExpr<T> CompileCompendiumEntry<T>(JsonElement element) where T : class
    {
        Expr<string> idExpr = CompileString(element);
        return new CompendiumEntryExpr<T>(idExpr);
    }
}
