using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Rpg;

public class SkillJson
{
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
    
    public SkillJson(JsonObject obj)
    {
        if (SidedLogic.Instance.IsClient()) return;
        
        string? executeCode = obj["execute"]?.GetValue<string>();
        string? startCode = obj["start"]?.GetValue<string>();
        string? cancelCode = obj["cancel"]?.GetValue<string>();
        string? canCancelCode = obj["canCancel"]?.GetValue<string>();
        string? delayCode = obj["delay"]?.GetValue<string>();
        string? cooldownCode = obj["cooldown"]?.GetValue<string>();
        string? durationCode = obj["duration"]?.GetValue<string>();
        string? layersCode = obj["layers"]?.GetValue<string>();
        string? conditionCode = obj["condition"]?.GetValue<string>();
        string? damageCode = obj["damage"]?.GetValue<string>();
        string? doesHitCode = obj["doesHit"]?.GetValue<string>();
        string? onHitCode = obj["onHit"]?.GetValue<string>();
        string? onAttackCode = obj["onAttack"]?.GetValue<string>();
        string? canTargetCode = obj["canTarget"]?.GetValue<string>();
        Func<Context, T> Compile<T>(string code)
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
        if (executeCode != null) onExecute = Compile<object>(executeCode);
        if (startCode != null) onStart = Compile<object>(startCode);
        if (cancelCode != null) onCancel = Compile<object>(cancelCode);
        if (canCancelCode != null) canCancel = Compile<bool>(canCancelCode);
        if (delayCode != null) delay = Compile<uint>(delayCode);
        if (cooldownCode != null) cooldown = Compile<uint>(cooldownCode);
        if (durationCode != null) duration = Compile<uint>(durationCode);
        if (layersCode != null) layers = Compile<string[]>(layersCode);
        if (conditionCode != null) canExecute = Compile<bool>(conditionCode);
        
        if (onHitCode != null) onHit = Compile<object>(onHitCode);
        if (onAttackCode != null) onAttack = Compile<object>(onAttackCode);
        if (damageCode != null) damage = Compile<(float, DamageType)>(damageCode);
        if (doesHitCode != null) doesHit = Compile<(bool, string?)>(doesHitCode);
        if (conditionCode != null) condition = Compile<bool>(conditionCode);
        if (canTargetCode != null) canTarget = Compile<bool>(canTargetCode);
    }
}