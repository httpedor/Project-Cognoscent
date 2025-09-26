
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Rpg;
using TTRpgClient.scripts;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

[GlobalClass]
public partial class GameManager : Node
{

	private List<ClientBoard> boards = new();
    private Color defaultClearColor;

    public ClientBoard? CurrentBoard
    {
        get;
        set
        {
            VisionManager.ClearVisionPoints();
            if (field != null){
                field.UpdateTurnModeToast();
                field.Node.Visible = false;
            }
            field = value;
            if (field != null){
                
                field.Node.Visible = true;
                ChatControl.Instance.SetMessageHistory(field.GetChatHistory());
                if (field.TurnMode)
                {
                    InitiativeBar.PopulateWithBoard(field);
                    InitiativeBar.Show();
                }
                else
                    InitiativeBar.Hide();
                field.UpdateTurnModeToast();
                if (IsGm) return;
                
                RenderingServer.SetDefaultClearColor(new Color(0, 0, 0));
                var creatures = field.GetCreaturesByOwner(Username);
                foreach (Creature creature in creatures)
                    VisionManager.AddVisionPoint(new VisionPoint(creature));
                if (creatures.Count == 0)
                {
                    foreach (var entity in field.GetEntities())
                    {
                        if (entity is Creature creature && creature.HasOwner())
                            VisionManager.AddVisionPoint(new VisionPoint(creature));
                    }
                }

            }
            else
            {
                InitiativeBar.Hide();
                ChatControl.Instance.SetMessageHistory(new List<string>());
                RenderingServer.SetDefaultClearColor(defaultClearColor);
                ActionBar.Clear();
                VisionManager.ClearVisionPoints();
            }
        }
    }

    public string Username = "";
    public InputManager InputManager
    {
        get;
        private set;
        
    }
    public SubViewport WorldViewport
    {
        get;
        private set;
    }
    public Node2D UINode
    {
        get;
        private set;
    }
    public Node2D BoardsNode
    {
        get;
        private set;
    }
    public VisionManager VisionManager
    {
        get;
        private set;
    }
    public static GameManager Instance {get; private set;}
    public static bool IsGm => Instance.Username.Equals("httpedor");

    public static CanvasLayer UILayer => Instance.GetParent().FindChild("UILayer") as CanvasLayer;

    public GameManager(){
        defaultClearColor = RenderingServer.GetDefaultClearColor();
        Instance = this;
        WorldViewport = new SubViewport
        {
            CanvasCullMask = 1,
            Name = "BoardViewport",
            PhysicsObjectPicking = true,
        };

        UINode = new Node2D
        {
            Name = "UINode",
            Material = new CanvasItemMaterial
            {
                LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
            },
            ZIndex = 20
        };

        BoardsNode = new Node2D
        {
            Name = "BoardsNode"
        };

        WorldViewport.AddChild(UINode);
        WorldViewport.AddChild(BoardsNode);

        InputManager = new InputManager();
        InputManager.AddChild(WorldViewport);

        VisionManager = new VisionManager();

        AddChild(VisionManager);
        AddChild(InputManager);
    }

    public void AddBoard(ClientBoard board){
        board.Node.Name = board.Name;
        board.Node.Visible = false;
        boards.Add(board);
        BoardsNode.AddChild(board.Node);
        if (CurrentBoard == null)
            CurrentBoard = board;
    }

    public ClientBoard? GetBoard(string name){
        return boards.Find(b => b.Name.Equals(name));
    }

    public IEnumerable<ClientBoard> GetBoards()
    {
        return boards;
    }

    public void RemoveBoard(ClientBoard? board){
        if (board == null)
            return;
        boards.Remove(board);
        BoardsNode.RemoveChild(board.Node);
        board.Node.QueueFree();
        if (CurrentBoard == board)
            CurrentBoard = null;
    }
    public void RemoveBoard(string name)
    {
        RemoveBoard(GetBoard(name));
    }
    public void ClearBoards(){
        foreach (var board in boards)
        {
            BoardsNode.RemoveChild(board.Node);
            board.Node.QueueFree();
        }
        boards.Clear();
        CurrentBoard = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        foreach (var board in boards)
        {
            Task.Run(async () =>
            {
                if (!board.TurnMode)
                    board.Tick();
            });
        }
    }

    public void ShowMenu(){
        var menu = GD.Load<PackedScene>("res://scenes/menu.tscn").Instantiate();
        menu.Set("network_manager", NetworkManager.Instance);
        menu.Set("game_manager", this);
        var ui = GetParent().FindChild("UILayer");
        if (ui.FindChild("Menu") != null)
            ui.FindChild("Menu").QueueFree();
        ui.AddChild(menu);

        BodyInspector.Instance.Hide();
        RadialMenu.Instance.Hide();
    }

    public void HideMenu()
    {
        GetParent().FindChild("UILayer").FindChild("Menu")?.QueueFree();
    }

    public void PlayAudio(Midia midia)
    {
        if (midia.Type != MidiaType.Audio)
            return;
        AudioStream stream;
        try
        {
            stream = new AudioStreamWav();
            ((AudioStreamWav)stream).SetData(midia.Bytes);
        }
        catch (Exception)
        {
            try
            {
                stream = AudioStreamOggVorbis.LoadFromBuffer(midia.Bytes);
            }
            catch (Exception)
            {
                return;
            }
        }
        PlayAudio(stream);
    }

    public void PlayAudio(AudioStream stream)
    {
        var player = new AudioStreamPlayer();
        player.Stream = stream;
        UINode.AddChild(player);
        player.Play();
        player.Finished += () => player.QueueFree();
    }

    public void ExecuteCommand(string command)
    {
        string[] args = command.Split(" ")[1..];
        switch (command.Split(" ")[0].ToLower())
        {
            case "help":
                ChatControl.Instance.AddMessage("Commands: /help, /clear, /body, /grid");
                break;
            case "grid":
                if (CurrentBoard == null)
                {
                    ChatControl.Instance.AddMessage("No board selected");
                    return;
                }
                CurrentBoard.GridEnabled = !CurrentBoard.GridEnabled;
                break;
            case "clear":
                ChatControl.Instance.SetMessageHistory(new List<string>());
                break;
            case "body":
                if (CurrentBoard == null)
                {
                    ChatControl.Instance.AddMessage("No board selected");
                    return;
                }
                switch (CurrentBoard.SelectedEntity)
                {
                    case null:
                        ChatControl.Instance.AddMessage("No entity selected");
                        return;
                    case Creature creature:
                        ChatControl.Instance.AddMessage(creature.BodyRoot.PrintPretty());
                        break;
                    default:
                        ChatControl.Instance.AddMessage("Selected entity is not a creature");
                        break;
                }

                break;
            case "lighticons":
                LightNode.ShowLightIcons = !LightNode.ShowLightIcons;
                break;
            case "gotoent":
            {
                if (CurrentBoard == null)
                {
                    ChatControl.Instance.AddMessage("No board selected");
                    break;
                }
                int id = int.Parse(args[0]);
                Entity? entity = CurrentBoard.GetEntityById(id);
                if (entity == null)
                    break;
                CurrentBoard.CenterOn(entity);
                break;
            }
        }
    }
}