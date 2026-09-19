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

    [ExprOp("log", Description = "Writes a message to the server log.")]
    public static LogEffectExpr Op(
        [Doc("Message to log")] Expr<string> message,
        [Doc("Log level; defaults to Info")] Expr<LogLevel>? level = null)
        => new LogEffectExpr(message, level);
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

    [ExprOp("chat_message", "chat", "log_to_chat",
            Description = "Posts a message to the board chat.")]
    public static ChatMessageExpr Op(
        [Doc("Message to post")] Expr<string> message,
        [Doc("Entities whose boards receive it; broadcasts when omitted")] ArrayExpr<Entity?>? targets = null)
        => new ChatMessageExpr(message, targets);
}