using Godot;
using System;

public partial class CameraBound : Node2D
{
    // Draw this rect in the editor to define camera limits
    [Export] public Vector2 Size { get; set; } = new Vector2(1280, 720);

	public override void _Ready()
	{
		var camera = GetTree().GetFirstNodeInGroup("camera") as Camera2D;
		if (camera == null) return;

		Vector2 topLeft = GlobalPosition;
		Vector2 bottomRight = GlobalPosition + Size;

		camera.LimitLeft = (int)topLeft.X;
		camera.LimitTop = (int)topLeft.Y;
		camera.LimitRight = (int)bottomRight.X;
		camera.LimitBottom = (int)bottomRight.Y;
	}

	// Draw the bounds visually in editor
	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 1, 0, 0.3f), false, 2f);
	}
}

