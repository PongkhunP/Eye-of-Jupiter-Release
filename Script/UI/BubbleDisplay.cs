using Godot;
using System;

public partial class BubbleDisplay : HBoxContainer
{
    [Export] public Texture2D FullBubble;
    [Export] public Texture2D EmptyBubble;
    [Export] public int MaxBubbles = 5;
    [Export] public float FadeDuration = 0.4f;

    private TextureRect[] _bubbles;
    private Tween _tween;

    public override void _Ready()
    {
        var stats = PlayerStatManager.Instance;
        if (stats == null)
        {
            GD.PrintErr("BubbleDisplay: PlayerStatManager not found!");
            return;
        }

        BuildBubbles();
        stats.O2Changed += OnO2Changed;
        OnO2Changed(stats.O2, stats.MaxO2);
    }

    public override void _ExitTree()
    {
        if (PlayerStatManager.Instance != null)
            PlayerStatManager.Instance.O2Changed -= OnO2Changed;
    }

    private void BuildBubbles()
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        _bubbles = new TextureRect[MaxBubbles];

        for (int i = 0; i < MaxBubbles; i++)
        {
            var container = new Control();
            container.CustomMinimumSize = FullBubble?.GetSize() ?? new Vector2(32, 32);

            // Empty bubble underneath
            var empty = new TextureRect();
            empty.Texture = EmptyBubble;
            empty.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            empty.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            container.AddChild(empty);

            // Full bubble on top — this one fades
            var full = new TextureRect();
            full.Texture = FullBubble;
            full.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            full.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            container.AddChild(full);

            _bubbles[i] = full;
            AddChild(container);
        }
    }

    private void OnO2Changed(float current, float max)
    {
        if (_bubbles == null || !IsInsideTree()) return;
        if (_bubbles == null) return;

        float o2PerBubble = max / MaxBubbles;

        _tween?.Kill();
        _tween = CreateTween();
        _tween.SetParallel(true);

        for (int i = 0; i < MaxBubbles; i++)
        {
            float bubbleMin = i * o2PerBubble;
            float fill = Mathf.Clamp((current - bubbleMin) / o2PerBubble, 0f, 1f);

            _tween.TweenProperty(
                _bubbles[i], "modulate:a",
                fill,
                FadeDuration
            );
        }
    }
}
