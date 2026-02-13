using System.Numerics;
using Rpg.Entities;
using Rpg.Entities.Components;

namespace Rpg.Scripting;


public sealed class DistanceSelectorExpr : SelectorExpr
{
    public readonly SelectorExpr From;
    public readonly NumberExpr? RangeExpr;
    public readonly bool Nearest;
    public DistanceSelectorExpr(SelectorExpr from, NumberExpr? rangeExpr, bool nearest)
    {
        From = from;
        RangeExpr = rangeExpr;
        Nearest = nearest;
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
}