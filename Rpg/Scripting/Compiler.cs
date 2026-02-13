using System.Text.Json;
using Rpg.Entities;

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
    public Board? Board {get; init;} = null;
    /// <summary>
    /// The entity executing the script, if any.
    /// </summary>
    public Entity? Caller {get; init;} = null;
    /// <summary>
    /// The target entity of the script, if any.
    /// </summary>
    public Entity? Target {get; init;} = null;
    /// <summary>
    /// The exact bodypart of the target entity being affected, if any.
    /// </summary>
    public Entity? TargetPart {get; init;} = null;

    public EvalContext()
    {
        Variables = Array.Empty<object>();
    }
    public EvalContext(Dictionary<string, object> variables, CompileContext compileCtx)
    {
        Variables = new object[compileCtx.Symbols.Count];
        foreach (var kvp in variables)
        {
            int symbolId = compileCtx.GetSymbol(kvp.Key);
            Variables[symbolId] = kvp.Value;
        }
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

    public T GetVariable<T>(int index)
    {
        return (T)Variables[index];
    }
}

//TODO: Use this in all stuff in the compendium
public sealed class CompileContext
{
    private readonly static Dictionary<string, string> _aliases = new()
    {
        {"STR", "STRENGTH"},
        {"DEX", "DEXTERITY"},
        {"CON", "CONSTITUTION"},
        {"INT", "INTELLIGENCE"},
        {"WIS", "WISDOM"},
        {"CHA", "CHARISMA"},
        {"HP", "HEALTH"},
        {"MP", "MANA"},
        {"SP", "STAMINA"},
    };
    private readonly Dictionary<string, int> _symbols = new();

    public int GetSymbol(string name)
    {
        name = name.ToUpper();
        if (_aliases.TryGetValue(name, out string? alias))
        {
            name = alias;
        }
        if (!_symbols.TryGetValue(name, out int id))
        {
            id = _symbols.Count;
            _symbols[name] = id;
        }
        return id;
    }

    public IReadOnlyDictionary<string, int> Symbols => _symbols;
    private T[] CompileArgsAs<T>(JsonElement array) where T : BaseExpr
    {
        var list = new List<T>();
        foreach (var el in array.EnumerateArray())
        {
            T compiled;
            if (typeof(T).IsAssignableTo(typeof(NumberExpr)))
                compiled = CompileNumber(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else if (typeof(T).IsAssignableTo(typeof(EffectExpr)))
                compiled = CompileEffect(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else if (typeof(T).IsAssignableTo(typeof(ConditionExpr)))
                compiled = CompileCondition(el) as T
                    ?? throw new Exception("Compiled expression is not of the expected type.");
            else
                throw new Exception("Unsupported expression type for compilation.");
            list.Add(compiled);
        }
        return list.ToArray();
    }
    public NumberExpr CompileNumber(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return new ConstExpr(element.GetSingle());

            case JsonValueKind.String:
            {
                string name = element.GetString()!;
                var randDenominators = new char[] {'d', 'D', '-', ':', ','};
                if (name.Length > 2 && name.IndexOfAny(randDenominators) >= 0)
                    return new RangeExpr(name);

                int symbolId = GetSymbol(name);
                return new VarExpr(symbolId);
            }

            case JsonValueKind.Object:
                var op = element.GetProperty("op").GetString();
                if (op == null)
                    throw new Exception("NumberExpression object missing 'op' property.");

                return CompileNumberObj(element, op.ToLower());

            default:
                throw new Exception($"Invalid expression json: {element}");
        }
    }
    private NumberExpr CompileNumberObj(JsonElement obj, string op)
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
                SelectorExpr entityName = obj.TryGetProperty("entity", out var entityNameElement) ? CompileSelector(entityNameElement) : new CallerSelectorExpr();
                NumberExpr defaultValue = obj.TryGetProperty("default", out var defaultValueElement) ? CompileNumber(defaultValueElement) : new ConstExpr(0);
                return new StatExpr(statName, entityName, defaultValue);
            }

            case "sum":
            case "plus":
            case "add":
            case "addition":
            case "+":
                return new AddExpr(CompileArgsAs<NumberExpr>(obj.GetProperty("numbers")));

            case "sub":
            case "subtract":
            case "minus":
            case "subtraction":
            case "-":
                return new SubExpr(CompileArgsAs<NumberExpr>(obj.GetProperty("numbers")));

            case "mul":
            case "multiply":
            case "times":
            case "multiplication":
            case "*":
                return new MulExpr(CompileArgsAs<NumberExpr>(obj.GetProperty("numbers")));

            case "div":
            case "divide":
            case "division":
            case "/":
                return new DivExpr(CompileArgsAs<NumberExpr>(obj.GetProperty("numbers")));

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
    public EffectExpr CompileEffect(JsonElement element)
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
    private EffectExpr CompileEffectObj(JsonElement obj, string effectName)
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

    public ConditionExpr CompileCondition(JsonElement element)
    {
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
                    int symbolId = GetSymbol(name);
                    return new VarConditionExpr(symbolId);
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
    public ConditionExpr CompileConditionObj(JsonElement obj, string op)
    {
        var left = obj.GetProperty("left");
        var right = obj.GetProperty("right");
        switch (op)
        {
            case "and":
                return new AndConditionExpr(CompileArgsAs<ConditionExpr>(obj.GetProperty("conditions")));
            case "or":
                return new OrConditionExpr(CompileArgsAs<ConditionExpr>(obj.GetProperty("conditions")));
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
            case "var":
                {
                    string name = obj.GetProperty("name").GetString()!;
                    int symbolId = GetSymbol(name);
                    return new VarConditionExpr(symbolId);
                }
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

    public SelectorExpr CompileSelector(JsonElement element)
    {
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
                    _ => new VarEntitySelectorExpr(GetSymbol(name)),
                };
            }
            case JsonValueKind.Object:
            {
                string? name = element.GetProperty("selector").GetString();
                if (name == null)
                    throw new Exception("SelectorExpression object missing 'selector' property.");
                return CompileSelectorObj(element, name);
            }
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return new NoEntitySelectorExpr();
            default:
                throw new Exception($"Invalid selector element: {element}");
        }
    }
    public SelectorExpr CompileSelectorObj(JsonElement obj, string selectorName)
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
    public StringExpr CompileString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (element.GetString()?.StartsWith("$") == true)
                {
                    string varName = element.GetString()![1..];
                    int symbolId = GetSymbol(varName);
                    return new VarStringExpr(symbolId);
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
    private StringExpr CompileStringObj(JsonElement obj, string op)
    {
        switch (op)
        {
            case "argumentType":
            case "argType":
            case "argtype":
            case "argumenttype":
                var argName = obj.GetProperty("argument").GetString();
                if (argName == null)
                    throw new Exception("StringExpression with 'argumentType' operation missing 'argument' property.");
                int argumentIndex = GetSymbol(argName);
                return new ArgumentTypeNameExpr(argumentIndex);
            default:
                throw new Exception("Unknown string operation: " + op);
        }
    }

    public CompendiumEntryExpr<T> CompileCompendiumEntry<T>(JsonElement element) where T : class
    {
        StringExpr idExpr = CompileString(element);
        return new CompendiumEntryExpr<T>(idExpr);
    }
}