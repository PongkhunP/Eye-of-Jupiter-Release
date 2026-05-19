using Godot;

public partial class ScreenIndicator : Node2D
{
    public CameraController                Camera      { get; set; }
    public System.Func<ZeusBodyController> GetZeus     { get; set; }
    public float EdgePadding  { get; set; } = 40f;
    public float ArrowSize    { get; set; } = 20f;
    public Color ArrowColor   { get; set; } = new Color(1f, 0.85f, 0.1f, 0.9f);
    public bool  ShowDistance { get; set; } = true;

    public override void _Process(double delta)
    {
        var zeus = GetZeus?.Invoke();
        if (zeus != null && IsInstanceValid(zeus))
            QueueRedraw();
    }

    public override void _Draw()
    {
        var zeus = GetZeus?.Invoke();
        if (zeus == null || !IsInstanceValid(zeus) || Camera == null) return;

        Vector2 screenSize = GetViewportRect().Size;

        // World position → screen pixels via viewport transform only
        Transform2D vpTransform = Camera.GetViewportTransform();
        Vector2 zeusScreen = vpTransform * zeus.GlobalPosition;

        // Debug — remove after confirmed working
        // GD.Print($"Zeus screen pos: {zeusScreen}, Screen size: {screenSize}");

        var screenRect = new Rect2(Vector2.Zero, screenSize);
        if (screenRect.HasPoint(zeusScreen)) return;

        Vector2 center    = screenSize * 0.5f;
        Vector2 direction = (zeusScreen - center).Normalized();
        float   halfW     = center.X - EdgePadding;
        float   halfH     = center.Y - EdgePadding;

        Vector2 edgePos;
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
        {
            float x = direction.X > 0 ? halfW : -halfW;
            edgePos = new Vector2(x, x * (direction.Y / direction.X));
        }
        else
        {
            float y = direction.Y > 0 ? halfH : -halfH;
            edgePos = new Vector2(y * (direction.X / direction.Y), y);
        }
        edgePos += center;

        DrawArrow(edgePos, direction);

        if (ShowDistance && PlayerController.Instance != null)
        {
            float dist = PlayerController.Instance.GlobalPosition
                         .DistanceTo(zeus.GlobalPosition);
            DrawString(
                ThemeDB.FallbackFont,
                edgePos + direction * (ArrowSize + 8f),
                $"{Mathf.RoundToInt(dist)}m",
                HorizontalAlignment.Center,
                -1, 12, ArrowColor
            );
        }
    }

    private void DrawArrow(Vector2 position, Vector2 direction)
    {
        Vector2 tip   = position + direction * ArrowSize;
        Vector2 perp  = direction.Rotated(Mathf.Pi * 0.5f) * (ArrowSize * 0.5f);
        Vector2 baseL = position - direction * (ArrowSize * 0.3f) + perp;
        Vector2 baseR = position - direction * (ArrowSize * 0.3f) - perp;

        DrawColoredPolygon(new Vector2[] { tip, baseL, baseR }, ArrowColor);
        DrawPolyline(
            new Vector2[] { tip, baseL, baseR, tip },
            new Color(ArrowColor.R, ArrowColor.G, ArrowColor.B, 1f),
            2f, true
        );

        float pulse = (Mathf.Sin(Time.GetTicksMsec() * 0.005f) + 1f) * 0.5f;
        DrawCircle(position, 5f + pulse * 3f,
            new Color(ArrowColor.R, ArrowColor.G, ArrowColor.B, 0.5f));
    }
}