using Godot;
using System;

public partial class BubbleDisplay : HBoxContainer
{
	[Export] public Texture2D FullBubble;
    [Export] public Texture2D EmptyBubble;
    [Export] public int MaxBubbles = 5;

    public override void _Ready()
    {
        var stats = PlayerStatManager.Instance;
        if (stats == null)
        {
            GD.PrintErr("BubbleDisplay: PlayerStatManager not found!");
            return;
        }

        stats.O2Changed += OnO2Changed;

        // Draw immediately with current values
        OnO2Changed(stats.O2, stats.MaxO2);
    }

    private void OnO2Changed(float current, float max)
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        int filled = Mathf.RoundToInt((current / max) * MaxBubbles);

        for (int i = 0; i < MaxBubbles; i++)
        {
            var icon = new TextureRect();
            icon.Texture = i < filled ? FullBubble : EmptyBubble;
            AddChild(icon);
        }
    }
}
