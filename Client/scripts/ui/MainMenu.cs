using Godot;

namespace TTRpgClient.scripts.ui;

public partial class MainMenu : Control
{
    [Export] private NetworkManager networkManager;
    [Export] private GameManager gameManager;

    private Label errorLabel;

    public override void _Ready()
    {
        var control = GetNode("Control");
        var usernameLine = control.GetNode<LineEdit>("UsernameLine");
        var ipLine = control.GetNode<LineEdit>("IpLine");
        var button = control.GetNode<Button>("Button");
        errorLabel = control.GetNode<Label>("ErrLabel");

        button.Pressed += () => OnConnect(usernameLine.Text, ipLine.Text);
    }

    private void OnConnect(string username, string ip)
    {
        var split = ip.Split(":");
        if (split.Length != 2)
        {
            errorLabel.Text = "Invalid IP";
            return;
        }

        var ipAddress = split[0];
        var port = split[1].ToInt();
        gameManager._username = username;
        var err = networkManager.ConnectToHost(ipAddress, port);
        if (err != Error.Ok)
            errorLabel.Text = $"Error connecting: {err}";
        else
            QueueFree();
    }
}
