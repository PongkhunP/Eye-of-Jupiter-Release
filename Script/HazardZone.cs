using Godot;

/// <summary>
/// Area2D hazard (lightning storm, acid rain). Overlaps <see cref="PlayerController"/>'s HazardDetector.
/// Collision: layer = Hazard (2); player detector mask must include this layer.
/// </summary>
public partial class HazardZone : Area2D
{
	[Export] public float O2DrainPerSecond { get; set; } = 8f;
	[Export] public float HpDrainPerSecond { get; set; } = 5f;

	private bool _playerInside = false;

	public override void _Ready()
	{
		Monitoring = true;   // ← must be true to detect overlaps
		Monitorable = true;

		CollisionLayer = 4;
		CollisionMask = 4;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		AddToGroup("hazard");
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInside = true;
			GD.Print("Player entered hazard zone");
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInside = false;
			GD.Print("Player exited hazard zone");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_playerInside) return;

		var stats = PlayerStatManager.Instance;
		if (stats == null) return;

		stats.Tick((float)delta, O2DrainPerSecond, HpDrainPerSecond);
	}
}
