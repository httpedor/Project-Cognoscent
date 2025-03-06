using System.Reflection;
using System.Runtime.Serialization;

namespace Rpg;

public enum AnimationId
{

}
public abstract class Animation : ISerializable
{
    private static Dictionary<AnimationId, Type> animationTypes = new Dictionary<AnimationId, Type>();
    static Animation(){
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()){
            if (type.IsSubclassOf(typeof(Packet))){
                var instance = FormatterServices.GetUninitializedObject(type) as Animation;
                if (instance != null)
                    animationTypes.Add(instance.Id, type);
            }
        }
    }

    public abstract AnimationId Id {get;}

    public static Animation FromBytes(Stream stream)
    {
        var id = (AnimationId)(Byte)stream.ReadByte();

        if (animationTypes.ContainsKey(id)){
            return (Animation)Activator.CreateInstance(animationTypes[id], new object[]{stream});
        }
        throw new Exception("Unknown packet id " + id);
    }

    public virtual void ToBytes(Stream stream)
    {
        stream.WriteByte((Byte)Id);
    }
}
