using System.Net;
using System.Net.Sockets;
using Server;
using Server.Game;
using Server.TUI;

#if ENABLE_WEB
using Server.Web;
#endif

TUI.Init();
ServerSidedLogic.Init();
Game.Init();
Command.Init();

// Start web host (conditional)
using var cts = new CancellationTokenSource();
#if ENABLE_WEB
var webHostTask = WebHost.StartAsync(cts.Token);
#endif

IPEndPoint iPEndPoint = new(IPAddress.Any, 25565);
Socket server = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
server.Bind(iPEndPoint);
server.Listen(100);

#if ENABLE_WEB
Loggers.Web.Log("Web Server started on localhost:5000");
#endif
Loggers.Console.Log("Server started on " + iPEndPoint);

try
{
    while (true)
    {
        var socket = await server.AcceptAsync();
        _ = Task.Run(() => Server.Network.Manager.NewConnection(socket));
    }
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    // shutting down
}
finally
{
    // request web host shutdown
    cts.Cancel();
#if ENABLE_WEB
    try
    {
        var host = await webHostTask;
        await host.StopAsync();
    }
    catch { }
#endif
}