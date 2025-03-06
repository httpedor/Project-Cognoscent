namespace Rpg;

public interface ISerializable
{

    public void ToBytes(Stream stream);

    public byte[] ToBytes(){
        using (var stream = new MemoryStream()){
            ToBytes(stream);
            return stream.ToArray();
        }
    }
}
