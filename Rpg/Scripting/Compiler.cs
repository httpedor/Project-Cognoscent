using System.Text.Json;
using Rpg.Entities;
using Rpg.Entities.Components;

namespace Rpg.Scripting;
public sealed class EvalContext
{
    /// <summary>
    /// Array of variable values indexed by symbol IDs.
    /// </summary>
    public object[] Variables {get; init;}
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
    public Entity? TargetPart = null;

    public EvalContext()
    {
        Variables = Array.Empty<object>();
    }
    public EvalContext(params object[] args)
    {
        Variables = args;
    }

    public T Eval<T>(Expr<T> expr)
    {
        return expr.Eval(this);
    }

    public EvalContext WithTarget(Entity? entity, Entity? caller = null, Entity? targetPart = null)
    {
        if (caller == null)
            caller = this.Caller;
        if (targetPart == null)
            targetPart = this.TargetPart;
        return new EvalContext
        {
            Variables = this.Variables,
            Board = this.Board,
            Caller = caller,
            Target = entity,
            TargetPart = targetPart
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
            TargetPart = this.TargetPart
        };
    }

    public T GetVariable<T>(int index)
    {
        return (T)Variables[index];
    }
}

//TODO: Use this in all stuff in the compendium
public static class ExpressionCompiler
{
    private static T[] CompileArgsAs<T>(JsonElement array) where T : BaseExpr
    {
        var list = new List<T>();
        foreach (var el in array.EnumerateArray())
        {
            T compiled;
            if (typeof(T).IsAssignableTo(typeof(Expr<float>)))
                compiled = CompileNumber(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else if (typeof(T).IsAssignableTo(typeof(EffectExpr)))
                compiled = CompileEffect(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else if (typeof(T).IsAssignableTo(typeof(Expr<bool>)))
                compiled = CompileCondition(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else
                throw new Exception("Unsupported expression type for compilation.");
            list.Add(compiled);
        }
        return list.ToArray();
    }
    private static Expr<T>? CheckForIf<T>(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("op", out var opElement) && opElement.ValueKind == JsonValueKind.String && opElement.GetString()?.ToLower() == "if")
        {
            var condition = CompileCondition(element.GetProperty("condition"));
            var trueExpr = Compile<T>(element.GetProperty("true"));
            var falseExpr = Compile<T>(element.GetProperty("false"));
            return new ConditionalExpr<T>(condition, trueExpr, falseExpr);
        }
        return null;
    }
    public static Expr<T> Compile<T>(JsonElement element)
    {
        var ifExpr = CheckForIf<T>(element);
        if (ifExpr != null)
            return ifExpr;
        if (typeof(T) == typeof(float))
            return CompileNumber(element) as Expr<T>
                ?? throw new Exception("Compiled expression is not of the expected type.");
        if (typeof(T) == typeof(bool))
            return CompileCondition(element) as Expr<T>
                ?? throw new Exception("Compiled expression is not of the expected type.");
        if (typeof(T) == typeof(Entity))
            return CompileSelector(element) as Expr<T>
                ?? throw new Exception("Compiled expression is not of the expected type.");
        if (typeof(T) == typeof(string))
            return CompileString(element) as Expr<T>
                ?? throw new Exception("Compiled expression is not of the expected type.");
        throw new Exception($"Unsupported expression type for compilation: {typeof(T)}");
    }
    public static Expr<float> CompileNumber(JsonElement element)
    {
        var ifExpr = CheckForIf<float>(element);
        if (ifExpr != null)
            return ifExpr;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new ConstNumberExpr(element.GetSingle());

            case JsonValueKind.String:
            {
                string name = element.GetString()!;
                var randDenominators = new char[] {'d', 'D', '-', ':', ','};
                if (name.Length > 2 && name.IndexOfAny(randDenominators) >= 0)
                    return new RangeExpr(name);
                if (name.StartsWith("$"))
                {
                    string varId = name[1..];
                    if (int.TryParse(varId, out int symbolId))
                    {
                        return new VarNumberExpr(symbolId);
                    }
                    else
                    {
                        throw new Exception($"Invalid variable ID: {varId}");
                    }
                }
                throw new Exception($"Invalid number expression string: {name}");
            }

            case JsonValueKind.Object:
                var op = element.GetProperty("op").GetString();
                if (op == null)
                    throw new Exception("Expr<float>ession object missing 'op' property.");

                return CompileNumberObj(element, op.ToLower());

            default:
                throw new Exception($"Invalid expression json: {element}");
        }
    }
    private static Expr<float> CompileNumberObj(JsonElement obj, string op)
    {
        switch (op)
        {
            case "lerp":
                return new LerpExpr(
                    CompileNumber(obj.GetProperty("min")),
                    CompileNumber(obj.GetProperty("max")),
                    CompileNumber(obj.GetProperty("t"))
                );
            case "rand":
            case "random":
            case "range":
                return new RangeExpr(
                    CompileNumber(obj.GetProperty("min")),
                    CompileNumber(obj.GetProperty("max"))
                );
            case "creature_stat":
            case "entity_stat":
            case "entitystat":
            case "creaturestat":
            case "stat":
            {
                string statName = obj.GetProperty("stat").GetString()!;
                Expr<Entity?> entityName = obj.TryGetProperty("entity", out var entityNameElement) ? CompileSelector(entityNameElement) : new CallerSelectorExpr();
                Expr<float> defaultValue = obj.TryGetProperty("default", out var defaultValueElement) ? CompileNumber(defaultValueElement) : new ConstNumberExpr(0);
                return new StatExpr(statName, entityName, defaultValue);
            }

            case "sum":
            case "plus":
            case "add":
            case "addition":
            case "+":
                return new AddExpr(CompileArgsAs<Expr<float>>(obj.GetProperty("numbers")));

            case "sub":
            case "subtract":
            case "minus":
            case "subtraction":
            case "-":
                return new SubExpr(CompileArgsAs<Expr<float>>(obj.GetProperty("numbers")));

            case "mul":
            case "multiply":
            case "times":
            case "multiplication":
            case "*":
                return new MulExpr(CompileArgsAs<Expr<float>>(obj.GetProperty("numbers")));

            case "div":
            case "divide":
            case "division":
            case "/":
                return new DivExpr(CompileArgsAs<Expr<float>>(obj.GetProperty("numbers")));

            case "run_script":
            case "runscript":
            case "invoke_script":
            case "invoke":
            case "invokescript":
            case "script":
                throw new NotSupportedException(
                    "Script invocation must be handled outside the expression system.");

            default:
                throw new Exception("Unknown operation: " + op);
        }
    }
    public static EffectExpr CompileEffect(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? name = element.GetProperty("effect").GetString();
                if (name == null)
                    throw new Exception("EffectExpression object missing 'effect' property.");

                return CompileEffectObj(
                    element,
                    name.ToLower());
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return new NoEffectExpr();
            default:
                throw new Exception($"Invalid effect element: {element}");
        }
    }
    private static EffectExpr CompileEffectObj(JsonElement obj, string effectName)
    {
        switch (effectName)
        {
            case "null":
            case "nop":
            case "noeffect":
            case "no_effect":
                return new NoEffectExpr();

            default:
                throw new Exception("Unknown effect: " + effectName);
        }
    }

    public static Expr<bool> CompileCondition(JsonElement element)
    {
        var ifExpr = CheckForIf<bool>(element);
        if (ifExpr != null)
            return ifExpr;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? op = element.GetProperty("op").GetString();
                if (op == null)
                    throw new Exception("ConditionExpression object missing 'op' property.");

                return CompileConditionObj(
                    element,
                    op.ToLower());
            case JsonValueKind.String:
                {
                    string name = element.GetString()!;
                    if (name == "true")
                        return new ConstConditionExpr(true);
                    if (name == "false")
                        return new ConstConditionExpr(false);
                    if (name.EndsWith("%"))
                    {
                        string probStr = name.Substring(0, name.Length - 1);
                        if (float.TryParse(probStr, out float probValue))
                        {
                            return new RandomConditionExpr(probValue / 100f);
                        }
                        else
                        {
                            throw new Exception($"Invalid probability value in condition: {name}");
                        }
                    }
                    if (name.StartsWith("$"))
                    {
                        string varId = name[1..];
                        if (int.TryParse(varId, out int symbolId))
                        {
                            return new VarConditionExpr(symbolId);
                        }
                        else
                        {
                            throw new Exception($"Invalid variable ID: {varId}");
                        }
                    }
                    throw new Exception($"Invalid condition string: {name}");
                }
            case JsonValueKind.Null:
                return new ConstConditionExpr(false);
            case JsonValueKind.Number:
                {
                    float value = element.GetSingle();
                    if (value > 1f)
                        new RandomConditionExpr(value/100f);
                    if (value < 0f)
                        throw new Exception($"Invalid probability value in condition: {value}");
                    return new RandomConditionExpr(value);
                }
            case JsonValueKind.True:
                return new ConstConditionExpr(true);
            case JsonValueKind.False:
                return new ConstConditionExpr(false);
            
            default:
                throw new Exception($"Invalid condition element: {element}");
        }
    }
    public static Expr<bool> CompileConditionObj(JsonElement obj, string op)
    {
        var left = obj.GetProperty("left");
        var right = obj.GetProperty("right");
        switch (op)
        {
            case "and":
                return new AndConditionExpr(CompileArgsAs<Expr<bool>>(obj.GetProperty("conditions")));
            case "or":
                return new OrConditionExpr(CompileArgsAs<Expr<bool>>(obj.GetProperty("conditions")));
            case "not":
                return new NotConditionExpr(
                    CompileCondition(obj.GetProperty("condition")));
            case "true":
                return new ConstConditionExpr(true);
            case "false":
                return new ConstConditionExpr(false);
            case ">":
                return new GreaterThanConditionExpr(
                    CompileNumber(left),
                    CompileNumber(right));
            case ">=":
                return new NotConditionExpr(
                    new LessThanConditionExpr(
                        CompileNumber(left),
                        CompileNumber(right)));
            case "<":
                return new LessThanConditionExpr(
                    CompileNumber(left),
                    CompileNumber(right));
            case "<=":
                return new NotConditionExpr(
                    new GreaterThanConditionExpr(
                        CompileNumber(left),
                        CompileNumber(right)));
            case "=":
            case "==":
                return new EqualConditionExpr(
                    CompileNumber(left),
                    CompileNumber(right));
            case "!=":
                return new NotEqualConditionExpr(
                    CompileNumber(left),
                    CompileNumber(right));
            case "random":
            case "rand":
                {
                    float probability = obj.TryGetProperty("probability", out var probElem) ? probElem.GetSingle() : 0.5f;
                    return new RandomConditionExpr(probability);
                }

            default:
                throw new Exception("Unknown condition operation: " + op);
        }
    }

    public static Expr<Entity?> CompileSelector(JsonElement element)
    {
        var ifExpr = CheckForIf<Entity?>(element);
        if (ifExpr != null)
            return ifExpr;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                string name = element.GetString()!;
                return name.ToLower() switch
                {
                    "self" => new CallerSelectorExpr(),
                    "caller" => new CallerSelectorExpr(),
                    "target" => new TargetSelectorExpr(),
                    "target_part" => new TargetPartSelectorExpr(),
                    _ => new VarEntitySelectorExpr(int.Parse(name[1..])),
                };
            }
            case JsonValueKind.Object:
            {
                string? name = element.GetProperty("op").GetString();
                if (name == null)
                    throw new Exception("SelectorExpression object missing 'op' property.");
                return CompileSelectorObj(element, name);
            }
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return new NoEntitySelectorExpr();
            default:
                throw new Exception($"Invalid selector element: {element}");
        }
    }
    public static Expr<Entity?> CompileSelectorObj(JsonElement obj, string selectorName)
    {
        switch (selectorName.ToLower())
        {
            case "caller":
            case "self":
                return new CallerSelectorExpr();
            case "target":
                return new TargetSelectorExpr();
            case "target_part":
                return new TargetPartSelectorExpr();
            default:
                throw new Exception("Unknown selector: " + selectorName);
        }
    }
    public static Expr<string> CompileString(JsonElement element)
    {
        var ifExpr = CheckForIf<string>(element);
        if (ifExpr != null)
            return ifExpr;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (element.GetString()?.StartsWith("$") == true)
                {
                    string varId = element.GetString()![1..];
                    if (int.TryParse(varId, out int symbolId))
                    {
                        return new VarStringExpr(symbolId);
                    }
                    else
                    {
                        throw new Exception($"Invalid variable ID: {varId}");
                    }
                }
                return new StringLiteralExpr(element.GetString()!);
            case JsonValueKind.Object:
                string? op = element.GetProperty("op").GetString();
                if (op == null)
                    throw new Exception("StringExpression object missing 'op' property.");

                return CompileStringObj(
                    element,
                    op.ToLower());
            default:
                throw new Exception($"Invalid string expression element: {element}");
        }
    }
    private static Expr<string> CompileStringObj(JsonElement obj, string op)
    {
        switch (op)
        {
            case "concat":
            case "add":
            case "join":
                return new StringConcatExpr(CompileArgsAs<Expr<string>>(obj.GetProperty("strings")));
            default:
                throw new Exception("Unknown string operation: " + op);
        }
    }

    public static CompendiumEntryExpr<T> CompileCompendiumEntry<T>(JsonElement element) where T : class
    {
        Expr<string> idExpr = CompileString(element);
        return new CompendiumEntryExpr<T>(idExpr);
    }
    public static EnumExpr<T> CompileEnum<T>(JsonElement element) where T : struct, Enum
    {
        Expr<string> valueExpr = CompileString(element);
        return new EnumExpr<T>(valueExpr);
    }
}