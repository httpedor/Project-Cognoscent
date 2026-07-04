using System.Text.Json;
using Rpg.Entities;

namespace Rpg.Scripting;

public sealed class LogEffectExpr : EffectExpr
{
    public readonly Expr<LogLevel> LevelExpr;
    public readonly Expr<string> MessageExpr;

    public LogEffectExpr(Expr<string> messageExpr, Expr<LogLevel>? levelExpr = null)
    {
        MessageExpr = messageExpr;
        LevelExpr = levelExpr ?? new EnumExpr<LogLevel>(LogLevel.Info);
    }
    public LogEffectExpr(Stream stream)
    {
        MessageExpr = (Expr<string>)BaseExpr.Deserialize(stream);
        LevelExpr = (Expr<LogLevel>)BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        string message = MessageExpr.Eval(ctx);
        Logger.Log(message, LevelExpr.Eval(ctx));
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        MessageExpr.ToBytes(stream);
        LevelExpr.ToBytes(stream);
    }

    [ExprOp(ExprCategory.Effect, "log")]
    [ExprParam("message", typeof(string), Required = true, Description = "The message to log when this effect is evaluated")]
    [ExprParam("level", typeof(LogLevel), Required = false, Description = "The log level")]
    public static LogEffectExpr Compile(JsonElement json)
    {
        var messageExpr = ExpressionCompiler.Compile<string>(json.GetProperty("message"));
        Expr<LogLevel>? levelExpr = null;
        if (json.TryGetProperty("level", out var levelEl))
        {
            levelExpr = ExpressionCompiler.Compile<LogLevel>(levelEl);
        }
        return new LogEffectExpr(messageExpr, levelExpr);
    }
}
public sealed class ChatMessageExpr : EffectExpr
{
    public readonly Expr<string> MessageExpr;
    public readonly ArrayExpr<Entity?>? TargetsExpr;

    public ChatMessageExpr(Expr<string> messageExpr, ArrayExpr<Entity?>? targetsExpr = null)
    {
        MessageExpr = messageExpr;
        TargetsExpr = targetsExpr;
    }
    public ChatMessageExpr(Stream stream)
    {
        MessageExpr = (Expr<string>)BaseExpr.Deserialize(stream);
        if (stream.ReadBoolean())
            TargetsExpr = (ArrayExpr<Entity?>)BaseExpr.Deserialize(stream);
    }

    public override void EvalEffect(EvalContext ctx)
    {
        string message = MessageExpr.Eval(ctx);
        if (TargetsExpr != null)
        {
            var targets = TargetsExpr.Eval(ctx);
            foreach (var target in targets)
            {
                if (target != null)
                    target.Board?.AddChatMessage(message);
            }
        }
        else
            ctx.Board?.BroadcastMessage(message);
    }
    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        MessageExpr.ToBytes(stream);
        if (TargetsExpr == null)
            stream.WriteBoolean(false);
        else
        {
            stream.WriteBoolean(true);
            TargetsExpr.ToBytes(stream);
        }
    }

    [ExprOp(ExprCategory.Effect, "chat_message", "chat", "log_to_chat")]
    [ExprParam("message", typeof(string), Required = true, Description = "The message to add to the chat when this effect is evaluated")]
    public static ChatMessageExpr Compile(JsonElement json)
    {
        var messageExpr = ExpressionCompiler.Compile<string>(json.GetProperty("message"));
        return new ChatMessageExpr(messageExpr);
    }
}