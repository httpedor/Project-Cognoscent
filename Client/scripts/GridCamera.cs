using Godot;
using System;

[GlobalClass]
public partial class GridCamera : Camera2D
{
    public static GridCamera Instance 
    {
        get;
        private set;
    }
	private Vector2 lastMousePos = new Vector2();
	private bool wasDragging = false;

    public GridCamera()
    {
        Instance = this;
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Input.IsActionPressed("drag_camera"))
        {
            Position -= (GetViewport().GetMousePosition() - lastMousePos) / Zoom;
            Input.SetDefaultCursorShape(Input.CursorShape.Drag);
            wasDragging = true;
        }
        else if (wasDragging)
        {
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            wasDragging = false;
        }

        if (Input.IsActionJustReleased("zoom_in") && !ChatControl.Instance.IsInputFocused)
        {
            Zoom *= 1.1f;
        }

        if (Input.IsActionJustReleased("zoom_out") && !ChatControl.Instance.IsInputFocused)
        {
            Zoom /= 1.1f;
        }

        lastMousePos = GetViewport().GetMousePosition();
	}
}
