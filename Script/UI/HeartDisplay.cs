using Godot;
using System;

public partial class HeartDisplay : HBoxContainer
{
	[Export] public Texture2D FullHeart;
	[Export] public Texture2D EmptyHeart;
	[Export] public int MaxHearts = 5;
	[Export] public float FadeDuration = 0.4f;

	private TextureRect[] _hearts;
	private Tween _tween;

	public override void _Ready()
	{
		var stats = PlayerStatManager.Instance;
		if (stats == null)
		{
			GD.PrintErr("HeartDisplay: PlayerStatManager not found!");
			return;
		}

		BuildHearts();
		stats.HpChanged += OnHpChanged;
		OnHpChanged(stats.Hp, stats.MaxHp);
	}

	private void BuildHearts()
	{
		// Clear existing
		foreach (Node child in GetChildren())
			child.QueueFree();

		_hearts = new TextureRect[MaxHearts];

		for (int i = 0; i < MaxHearts; i++)
		{
			// Stack full + empty heart on top of each other
			var container = new Control();
			container.CustomMinimumSize = FullHeart?.GetSize() ?? new Vector2(32, 32);

			// Empty heart underneath
			var empty = new TextureRect();
			empty.Texture = EmptyHeart;
			empty.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			empty.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			container.AddChild(empty);

			// Full heart on top — this one fades
			var full = new TextureRect();
			full.Texture = FullHeart;
			full.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			full.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			container.AddChild(full);

			_hearts[i] = full;
			AddChild(container);
		}
	}

	private void OnHpChanged(float current, float max)
	{
		if (_hearts == null) return;

		float hpPerHeart = max / MaxHearts;

		_tween?.Kill();
		_tween = CreateTween();
		_tween.SetParallel(true); // all hearts fade simultaneously

		for (int i = 0; i < MaxHearts; i++)
		{
			// How full is this heart? 0.0 = empty, 1.0 = full
			float heartMin = i * hpPerHeart;
			float heartMax = (i + 1) * hpPerHeart;
			float fill = Mathf.Clamp((current - heartMin) / hpPerHeart, 0f, 1f);

			_tween.TweenProperty(
				_hearts[i], "modulate:a",
				fill,           // target alpha
				FadeDuration
			);
		}
	}

	public override void _ExitTree()
	{
		if (PlayerStatManager.Instance != null)
			PlayerStatManager.Instance.HpChanged -= OnHpChanged;
	}
}
