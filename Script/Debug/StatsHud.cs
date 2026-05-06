using Godot;

/// <summary>
/// Minimal HUD to show PlayerStatManager HP/O2 while testing AI.
/// </summary>
public partial class StatsHud : CanvasLayer
{
	[Export] public NodePath O2LabelPath { get; set; }
	[Export] public NodePath HpLabelPath { get; set; }
	[Export] public NodePath HintLabelPath { get; set; }

	private Label _o2;
	private Label _hp;
	private Label _hint;

	public override void _Ready()
	{
		_o2 = !O2LabelPath.IsEmpty ? GetNodeOrNull<Label>(O2LabelPath) : null;
		_hp = !HpLabelPath.IsEmpty ? GetNodeOrNull<Label>(HpLabelPath) : null;
		_hint = !HintLabelPath.IsEmpty ? GetNodeOrNull<Label>(HintLabelPath) : null;

		if (_hint != null)
			_hint.Text = "WASD/Arrows to move. Enemy chases + hits. Zeus bolts + spawns hazards.";
	}

	public override void _Process(double delta)
	{
		var stats = PlayerStatManager.Instance;
		if (stats == null)
		{
			if (_o2 != null) _o2.Text = "O2: (no PlayerStatManager)";
			if (_hp != null) _hp.Text = "HP: (no PlayerStatManager)";
			return;
		}

		if (_o2 != null) _o2.Text = $"O2: {stats.O2:0}/{stats.MaxO2:0}";
		if (_hp != null) _hp.Text = $"HP: {stats.Hp:0}/{stats.MaxHp:0}";
	}
}

