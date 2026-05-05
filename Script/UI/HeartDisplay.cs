using Godot;
using System;

public partial class HeartDisplay : HBoxContainer
{
	[Export] public Texture2D FullHeart;
	[Export] public Texture2D EmptyHeart;
	[Export] public int MaxHearts = 5;

	public override void _Ready()
	{
		var stats = PlayerStatManager.Instance;
		if (stats == null)
		{
			GD.PrintErr("HeartDisplay: PlayerStatManager not found!");
			return;
		}

		stats.HpChanged += OnHpChanged;

		OnHpChanged(stats.Hp, stats.MaxHp);
	}

	private void OnHpChanged(float current, float max)
	{
		// Clear old icons
		foreach (Node child in GetChildren())
			child.QueueFree();

		int filled = Mathf.RoundToInt((current / max) * MaxHearts);

		for (int i = 0; i < MaxHearts; i++)
		{
			var icon = new TextureRect();
			icon.Texture = i < filled ? FullHeart : EmptyHeart;
			AddChild(icon);
		}
	}
}
