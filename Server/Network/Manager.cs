using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg;
using Server.Game;
using Server.TUI;

namespace Server.Network;

public static class Manager
{
    public static readonly Dictionary<string, RpgClient> Clients = new();

    public static void Disconnect(string username, bool sendDisconnect = true)
    {
        if (!Clients.TryGetValue(username, out RpgClient? client)) return;

        client.Disconnect();
    }

    public static RpgClient? GetClient(string username)
    {
        return Clients.GetValueOrDefault(username);
    }

    public static void SendToAll(Packet packet)
    {
        foreach (RpgClient? client in Clients.Values)
        {
            client.Send(packet);
        }
    }
    public static void Broadcast(Packet packet)
    {
        SendToAll(packet);
    }

    public static void SendToSome(Packet packet, Func<RpgClient, bool> predicate)
    {
        foreach (RpgClient? client in Clients.Values)
        {
            if (predicate(client))
                client.Send(packet);
        }
    }
    public static void SendToBoard(Packet packet, string boardName)
    {
        foreach (RpgClient? client in Clients.Values)
        {
            if (client.LoadedBoards.Contains(boardName))
                client.Send(packet);
        }
    }
    public static void SendToOthersInBoard(Packet packet, string boardname, string username)
    {
        foreach (RpgClient? client in Clients.Values)
        {
            if (client.LoadedBoards.Contains(boardname) && client.Username != username)
                client.Send(packet);
        }
    }
    public static void SendToOthersInBoard(Packet packet, Board board, RpgClient client)
    {
        SendToOthersInBoard(packet, board.Name, client.Username);
    }
    public static void SendToBoard(Packet packer, ServerBoard board)
    {
        SendToBoard(packer, board.Name);
    }

    public static void SendToOthers(Packet packet, string username)
    {
        foreach (RpgClient? client in Clients.Values)
        {
            if (client.Username != username)
                client.Send(packet);
        }
    }

    public static void SendTo(Packet packet, string username)
    {
        if (Clients.TryGetValue(username, out RpgClient? value))
        {
            value.Send(packet);
        }
    }

    public static void NewConnection(Socket socket)
    {
        RpgClient client = new(socket);
        Task.Run(() =>
        {
            byte[] buffer = new byte[1024];
            MemoryStream socketStream = new();
            uint expectedBufferLength = 0;
            while (client.Connected)
            {
                try
                {
                    int received = client.socket.Left!.Receive(buffer);
                    if (received == 0)
                    {
                        continue;
                    }

                    socketStream.Seek(0, SeekOrigin.End);
                    socketStream.Write(buffer, 0, received);

                    while ((expectedBufferLength != 0 && socketStream.Length >= expectedBufferLength) || (expectedBufferLength == 0 && socketStream.Length >= 5))
                    {
                        if (expectedBufferLength == 0)
                        {
                            socketStream.Seek(0, SeekOrigin.Begin);
                            uint length = socketStream.ReadUInt32();
                            byte id = (byte)socketStream.ReadByte();
                            expectedBufferLength = length;
                        }

                        if (socketStream.Length >= expectedBufferLength)
                        {
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

                }
                catch (Exception e)
                {
                    Logger.LogError(e.ToString());
                    if (e is SocketException)
                        Disconnect(client.Username);
                }
            }
        });
    }

    public static Task NewConnection(WebSocket webSocket)
    {
        RpgClient client = new(webSocket);
        return Task.Run(async () =>
        {
            byte[] buffer = new byte[1024 * 4];
            while (client.Connected)
            {
                try
                {
                    await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    string json = System.Text.Encoding.UTF8.GetString(buffer);
                    json = json.Trim('\0');
                    Packet packet = Packet.ReadPacketJson(json);
                    client.HandlePacket(packet);
                    Array.Clear(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    Loggers.Web.Log(e.ToString(), Rpg.LogLevel.Error);
                    if (webSocket.CloseStatusDescription != null)
                        Loggers.Web.Log(webSocket.CloseStatusDescription);
                    client.Disconnect(false);
                }
            }
        });
    }
}
