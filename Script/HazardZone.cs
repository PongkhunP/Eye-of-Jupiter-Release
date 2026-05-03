using Godot;

/// <summary>
/// Area2D hazard (lightning storm, acid rain). Overlaps <see cref="PlayerController"/>'s HazardDetector.
/// Collision: layer = Hazard (2); player detector mask must include this layer.
/// </summary>
public partial class HazardZone : Area2D
{
	[Export] public float O2DrainPerSecond { get; set; } = 8f;

	[Export] public float HpDrainPerSecond { get; set; } = 5f;

	public override void _Ready()
	{
		CollisionLayer = 2;
		CollisionMask = 0;
		Monitoring = false;
		Monitorable = true;
		AddToGroup("hazard");
	}
}
