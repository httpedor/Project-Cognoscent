using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Rpg;

public class SkillJson
{
    private JsonObject _obj;
    public class Context
    {
        public Creature? executor;

        public Creature? creature
        {
            get => executor;
            set => executor = value;
        }
        public IDamageable? target;
        public List<SkillArgument>? arguments;
        public ISkillSource? source;
        public uint? tick;
        public bool? interrupted;
    }
    public readonly Func<Context, object>? onExecute;
    public readonly Func<Context, object>? onStart;
    public readonly Func<Context, object>? onCancel;
    public readonly Func<Context, bool>? canCancel;
    public readonly Func<Context, uint>? delay;
    public readonly Func<Context, uint>? cooldown;
    public readonly Func<Context, uint>? duration;
    public readonly Func<Context, bool>? canExecute;
    public readonly Func<Context, string[]>? layers;
    
    public readonly Func<Context, (float, DamageType)>? damage;
    public readonly Func<Context, (bool, string?)>? doesHit;
    public readonly Func<Context, object>? onHit;
    public readonly Func<Context, object>? onAttack;
    public readonly Func<Context, bool>? condition;
    public readonly Func<Context, bool>? canTarget;
    
    private Func<Context, T> Compile<T>(string code)
    {
        try
        {
            var script = CSharpScript.Create<T>(code,
                ScriptOptions.Default.WithReferences(typeof(Creature).Assembly).WithImports("Rpg", "System", "System.Linq"),
                typeof(Context)).CreateDelegate();
            return ctx => script(ctx).Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Could not compile script: " + code, e);
        }
    }

    private Func<Context, T>? TryCompile<T>(string name, bool wrap = true)
    {
        string? code = _obj[name]?.GetValue<string>();
        if (code == null) return null;
        if (wrap)
            code = $"({typeof(T).Name})({code})";
        return Compile<T>(code);
    }
    public SkillJson(JsonObject obj)
    {
        if (SidedLogic.Instance.IsClient()) return;

        _obj = obj;
        // Basic skill properties
        onExecute = TryCompile<object>("execute", false);
        onStart = TryCompile<object>("start", false);
        onCancel = TryCompile<object>("cancel", false);
        canCancel = TryCompile<bool>("canCancel");
        delay = TryCompile<uint>("delay");
        cooldown = TryCompile<uint>("cooldown");
        duration = TryCompile<uint>("duration");
        layers = TryCompile<string[]>("layers");
        canExecute = TryCompile<bool>("canExecute");
        
        // Attack skill properties
        onHit = TryCompile<object>("onHit", false);
        onAttack = TryCompile<object>("onAttack", false);
        damage = TryCompile<(float, DamageType)>("damage");
        doesHit = TryCompile<(bool, string?)>("doesHit");
        condition = TryCompile<bool>("condition");
        canTarget = TryCompile<bool>("canTarget");
    }
}