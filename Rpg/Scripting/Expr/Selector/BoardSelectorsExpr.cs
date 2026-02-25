using System.Numerics;
using Rpg.Entities;
using Rpg.Entities.Components;

namespace Rpg.Scripting;


public sealed class DistanceSelectorExpr : Expr<Entity?>
{
    public readonly Expr<Entity?> From;
    public readonly Expr<float>? RangeExpr;
    public readonly bool Nearest;
    public DistanceSelectorExpr(Expr<Entity?> from, Expr<float>? rangeExpr, bool nearest)
    {
        From = from;
        RangeExpr = rangeExpr;
        Nearest = nearest;
    }

    public DistanceSelectorExpr(Stream stream)
    {
        From = BaseExpr.Deserialize<Expr<Entity?>>(stream);
        if (stream.ReadBoolean())
            RangeExpr = BaseExpr.Deserialize<Expr<float>>(stream);
        Nearest = stream.ReadBoolean();
    }

    public override Entity? Eval(EvalContext ctx)
    {
        if (ctx.Board == null)
            return null;
        var fromEntity = From.Eval(ctx);
        if (fromEntity == null)
            return null;
        var token = fromEntity.Token;
        if (token == null)
            return null;

        var entities = ctx.Board.GetEntitiesWithComponent<Token>();
        Entity? nearest = null;
        float nearestDistance = Nearest ? float.MaxValue : float.MinValue;

        var range = RangeExpr?.Eval(ctx) ?? float.MaxValue;
        foreach (var entity in entities)
        {
            if (entity == fromEntity)
                continue;
            var targetToken = entity.Token;
            if (targetToken == null)
                continue;

            var distance = Vector3.Distance(token.Position, targetToken.Position);
            if (distance > range)
                continue;
            if (Nearest)
            {
                if (distance < nearestDistance)
                {
                    nearest = entity;
                    nearestDistance = distance;
                }
            }
            else
            {
                if (distance > nearestDistance)
                {
                    nearest = entity;
                    nearestDistance = distance;
                }
            }
        }
        return nearest;
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);
        From.ToBytes(stream);
        if (RangeExpr != null) {
            stream.WriteBoolean(true);
            RangeExpr.ToBytes(stream);
        }
        else
        {
            stream.WriteBoolean(false);
        }
        stream.WriteBoolean(Nearest);
    }
}