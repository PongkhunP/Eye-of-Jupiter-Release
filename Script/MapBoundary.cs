using Godot;
using System;

public partial class MapBoundary : StaticBody2D
{
	[Export] public TileMapLayer FloorLayer { get; set; }

    // Extra thickness of the wall so player can't clip through
    [Export] public float WallThickness { get; set; } = 64f;

    public override void _Ready()
    {
        if (FloorLayer == null)
        {
            GD.PrintErr("MapBoundary: FloorLayer not assigned.");
            return;
        }

        BuildWalls();
    }

    private void BuildWalls()
{
    Rect2I tileRect = FloorLayer.GetUsedRect();
    
    // Use GlobalPosition offset from FloorLayer
    Vector2 topLeft     = FloorLayer.ToGlobal(FloorLayer.MapToLocal(tileRect.Position));
    Vector2 bottomRight = FloorLayer.ToGlobal(FloorLayer.MapToLocal(tileRect.End));

    float width  = bottomRight.X - topLeft.X;
    float height = bottomRight.Y - topLeft.Y;
    float cx     = topLeft.X + width  * 0.5f;
    float cy     = topLeft.Y + height * 0.5f;

    // Top
    AddWall(new Vector2(cx, topLeft.Y - WallThickness * 0.5f),
            new Vector2(width + WallThickness * 2, WallThickness));
    // Bottom
    AddWall(new Vector2(cx, bottomRight.Y + WallThickness * 0.5f),
            new Vector2(width + WallThickness * 2, WallThickness));
    // Left
    AddWall(new Vector2(topLeft.X - WallThickness * 0.5f, cy),
            new Vector2(WallThickness, height));
    // Right
    AddWall(new Vector2(bottomRight.X + WallThickness * 0.5f, cy),
            new Vector2(WallThickness, height));

    GD.Print($"MapBoundary: walls built around {topLeft} → {bottomRight}");
}

private void AddWall(Vector2 globalPosition, Vector2 size)
{
    var shape = new CollisionShape2D();
    shape.Shape = new RectangleShape2D { Size = size };
    // Convert global position to local position of this node
    shape.Position = ToLocal(globalPosition);
    AddChild(shape);
}
}
