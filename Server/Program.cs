using System.Net;
using System.Net.Sockets;
using Rpg;
using Server;
using Server.Game;

ServerSidedLogic.Init();
Game.Init();
Command.Init();

IPEndPoint iPEndPoint = new(IPAddress.Any, 25565);
Socket server = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
server.Bind(iPEndPoint);
server.Listen(100);

Console.WriteLine("Server started on " + iPEndPoint);

while (true)
{
    await server.AcceptAsync().ContinueWith(task =>
    {
        Server.Network.Manager.NewConnection(task.Result);
    });
}

