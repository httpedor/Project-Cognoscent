using System.Net.Sockets;
using System.Reflection;
using Rpg;
using Server.Game;

namespace Server.Network;

public static class Manager
{
    public static readonly Dictionary<String, RpgClient> Clients = new();

    public static void Disconnect(String username, bool sendDisconnect = true){
        if (username != null && Clients.ContainsKey(username)){
            RpgClient client = Clients[username];
            if (client.socket.Connected){
                if (sendDisconnect){
                    try {
                        client.Send(new DisconnectPacket());
                    } catch (Exception) {}
                }
                client.socket.Disconnect(false);
                client.socket.Shutdown(SocketShutdown.Both);
                client.socket.Close();
            }
            Clients.Remove(username);
            Console.WriteLine(username + " disconnected");
        }
    }

    public static RpgClient? GetClient(String username){
        if (Clients.TryGetValue(username, out RpgClient? value))
        {
            return value;
        }
        return null;
    }

    public static void SendToAll(Packet packet){
        foreach (var client in Clients.Values)
        {
            client.Send(packet);
        }
    }
    public static void Broadcast(Packet packet)
    {
        SendToAll(packet);
    }

    public static void SendToSome(Packet packet, Func<RpgClient, bool> predicate){
        foreach (var client in Clients.Values)
        {
            if (predicate(client))
                client.Send(packet);
        }
    }
    public static void SendToBoard(Packet packet, string boardName){
        foreach (var client in Clients.Values)
        {
            if (client.LoadedBoards.Contains(boardName))
                client.Send(packet);
        }
    }
    public static void SendToBoard(Packet packer, ServerBoard board){
        SendToBoard(packer, board.Name);
    }

    public static void SendToOthers(Packet packet, string username){
        foreach (var client in Clients.Values)
        {
            if (client.Username != username)
                client.Send(packet);
        }
    }

    public static void SendTo(Packet packet, string username){
        if (Clients.TryGetValue(username, out RpgClient? value))
        {
            value.Send(packet);
        }
    }

    public static void NewConnection(Socket socket){
        RpgClient client = new(socket);
        Task.Run(() => {
            byte[] buffer = new byte[1024];
            MemoryStream socketStream = new();
            uint expectedBufferLength = 0;
            while (client.socket.Connected){
                try{
                    int received = client.socket.Receive(buffer);
                    if (received == 0){
                        continue;
                    }

                    socketStream.Seek(0, SeekOrigin.End);
                    socketStream.Write(buffer, 0, received);

                    while ((expectedBufferLength != 0 && socketStream.Length >= expectedBufferLength) || (expectedBufferLength == 0 && socketStream.Length >= 5)){
                        if (expectedBufferLength == 0){
                            socketStream.Seek(0, SeekOrigin.Begin);
                            UInt32 length = socketStream.ReadUInt32();
                            byte id = (byte)socketStream.ReadByte();
                            expectedBufferLength = length;
                        }

                        if (socketStream.Length >= expectedBufferLength){
                            //Read the packet
                            socketStream.Seek(0, SeekOrigin.Begin);
                            byte[] packet = new byte[expectedBufferLength];
                            socketStream.Read(packet, 0, (int)expectedBufferLength);
                            
                            //Remove the packet from the buffer
                            byte[] under = socketStream.GetBuffer();
                            Array.Copy(under, expectedBufferLength, under, 0, under.Length - expectedBufferLength);
                            socketStream.SetLength(socketStream.Length - expectedBufferLength);

                            //Process the packet
                            Packet p = Packet.ReadPacket(packet);
                            if (packet != null)
                                client.HandlePacket(p);

                            //Reset the expected buffer length
                            expectedBufferLength = 0;
                        }
                    }

                } catch (Exception e){
                    Console.WriteLine(e);
                    if (e is SocketException)
                        Disconnect(client.Username);
                }
            }
        });
    }

}
