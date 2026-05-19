using Godot;

public partial class ZeusBodyController : Node2D
{
	[Export] public float MoveSpeed { get; set; } = 80f;
	[Export] public float StopThreshold { get; set; } = 10f;
	[Export] public float OrbitSpeed { get; set; } = 1.2f;
	[Export] public Node2D ArenaCenter { get; set; }
	[Export] public Vector2 ArenaSize { get; set; } = new Vector2(500f, 500f);

	[Export] private ZeusBrain brain;
	[Export] private AnimatedSprite2D _sprite;

	private BTBlackboard _blackboard;

	private float _orbitAngle = 0f;
	private float _orbitDirection = 1f;
	private float _orbitChangeCd = 0f;

	public override void _Ready()
	{
		if (brain == null)
		{
			GD.PrintErr($"{Name}: ZeusBrain not found!");
			return;
		}

		_blackboard = brain.Blackboard;

		if (_blackboard == null)
		{
			GD.PrintErr($"{Name}: Blackboard is null!");
			return;
		}

		_orbitAngle = (float)GD.RandRange(0.0, Mathf.Tau);
		_orbitDirection = GD.RandRange(0.0, 1.0) > 0.5 ? 1f : -1f;
		_orbitChangeCd = (float)GD.RandRange(3.0, 7.0);

		GD.Print($"{Name}: Ready — orbit dir: {(_orbitDirection > 0 ? "clockwise" : "counterclockwise")}");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_blackboard == null) return;

		bool isCasting = _blackboard.Get<bool>("is_casting");
		bool isStunned = _blackboard.Get<bool>("is_stunned");

		if (isCasting || isStunned)
		{
			PlayAnimation("idle");
			return;
		}
		PlayAnimation("idle");

		float dt = (float)delta;

		bool hasTarget = _blackboard.TryGet("target_position", out Vector2 target);
		bool hasPlayer = _blackboard.TryGet("player_position", out Vector2 playerPos);

		if (!hasTarget || !hasPlayer) return;

		float preferredDist = _blackboard.Get<float>("preferred_distance");
		float distToTarget = GlobalPosition.DistanceTo(target);

		if (distToTarget > StopThreshold)
		{
			// ── Chase: move toward maintain-distance target ───────
			Vector2 dir = (target - GlobalPosition).Normalized();
			GlobalPosition += dir * MoveSpeed * dt;
			// PlayAnimation("move");
			PlayAnimation("idle");

			// Sync orbit angle to current position around player
			// so orbit starts smoothly from wherever zeus ends up
			Vector2 toZeus = GlobalPosition - playerPos;
			_orbitAngle = Mathf.Atan2(toZeus.Y, toZeus.X);
		}
		else
		{
			// ── Orbit: circle around player ───────────────────────
			UpdateOrbitDirection(dt);

			_orbitAngle += OrbitSpeed * _orbitDirection * dt;

			Vector2 orbitTarget = playerPos + new Vector2(
				Mathf.Cos(_orbitAngle),
				Mathf.Sin(_orbitAngle)
			) * preferredDist;

			GlobalPosition = GlobalPosition.Lerp(orbitTarget, dt * 3f);
			// PlayAnimation("move");
			PlayAnimation("idle");
		}

		// ── Clamp to arena ────────────────────────────────────────
		if (_blackboard.TryGet("arena_center", out Vector2 arenaCenter)
		&& _blackboard.TryGet("arena_size", out Vector2 arenaSize))
		{
			Vector2 half = arenaSize * 0.5f;
			GlobalPosition = new Vector2(
				Mathf.Clamp(GlobalPosition.X, arenaCenter.X - half.X, arenaCenter.X + half.X),
				Mathf.Clamp(GlobalPosition.Y, arenaCenter.Y - half.Y, arenaCenter.Y + half.Y)
			);
		}

		// ── Face player ───────────────────────────────────────────
		if (_sprite != null)
			_sprite.FlipH = playerPos.X < GlobalPosition.X;
	}

	private void UpdateOrbitDirection(float dt)
	{
		_orbitChangeCd -= dt;
		if (_orbitChangeCd > 0f) return;

		_orbitDirection = GD.RandRange(0.0, 1.0) > 0.5 ? 1f : -1f;
		_orbitChangeCd = (float)GD.RandRange(3.0, 7.0);

		GD.Print($"[Zeus] Orbit: {(_orbitDirection > 0 ? "clockwise" : "counterclockwise")}");
	}

	private void PlayAnimation(string anim)
	{
		if (_sprite == null) return;

		// Map "move" to "idle" if move animation doesn't exist
		string resolved = _sprite.SpriteFrames.HasAnimation(anim) ? anim : "idle";

		if (_sprite.Animation == resolved && _sprite.IsPlaying()) return;

		_sprite.Play(resolved);
	}
}