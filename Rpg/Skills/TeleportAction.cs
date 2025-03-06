using System.Numerics;

namespace Rpg.Actions;

public class TeleportAction : Skill
{
    public Vector3 TargetPosition;

    public override String GetDescription()
    {
        return "Teleporta para um local";
    }

    public override String GetName()
    {
        return "Teleportar";
    }

    public override void ToBytes(Stream stream)
    {
        base.ToBytes(stream);

        stream.WriteVec3(TargetPosition);
    }
}
