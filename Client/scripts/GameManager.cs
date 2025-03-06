
using System.Collections.Generic;
using Godot;
using Rpg;
using Rpg.Entities;
using TTRpgClient.scripts;
using TTRpgClient.scripts.RpgImpl;
using TTRpgClient.scripts.ui;

[GlobalClass]
public partial class GameManager : Node
{

	private List<ClientBoard> boards = new List<ClientBoard>();
    private Color defaultClearColor;
    private ClientBoard? _currentBoard;
    public ClientBoard? CurrentBoard{
        get{
            return _currentBoard;
        }
        set {
            VisionManager.ClearVisionPoints();
            if (_currentBoard != null){
                _currentBoard.Node.Visible = false;
            }
            _currentBoard = value;
            if (_currentBoard != null){
                _currentBoard.Node.Visible = true;
                ChatControl.Instance.SetMessageHistory(_currentBoard.GetChatHistory());
                if (!IsGm){
                    RenderingServer.SetDefaultClearColor(new Color(0, 0, 0));
                    var creatures = _currentBoard.GetCreaturesByOwner(Username);
                    foreach (var creature in creatures)
                        VisionManager.AddVisionPoint(new VisionPoint(creature));
                    if (creatures.Count == 0)
                    {
                        foreach (var entity in _currentBoard.GetEntities())
                        {
                            if (entity is Creature creature && creature.HasOwner())
                                VisionManager.AddVisionPoint(new VisionPoint(creature));
                        }
                    }
                }
            }
            else
            {
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
        WorldViewport = new SubViewport()
        {
            CanvasCullMask = 1,
            Name = "BoardViewport",
            PhysicsObjectPicking = true,
        };

        UINode = new Node2D()
        {
            Name = "UINode",
            Material = new CanvasItemMaterial()
            {
                LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
            },
            ZIndex = 20
        };

        BoardsNode = new Node2D()
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

    public void ExecuteCommand(string command){
        switch (command)
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
                if (CurrentBoard.SelectedEntity == null)
                {
                    ChatControl.Instance.AddMessage("No entity selected");
                    return;
                }
                if (CurrentBoard.SelectedEntity is Creature creature)
                    ChatControl.Instance.AddMessage(creature.BodyRoot.PrintPretty());
                else
                    ChatControl.Instance.AddMessage("Selected entity is not a creature");
                break;
        }
    }
}