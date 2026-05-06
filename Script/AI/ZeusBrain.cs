using Godot;

/// <summary>
/// Sprint 2 Zeus behavior tree: phase-based lightning, arena control, punishment after failed trials.
/// Attach to a Node2D representing Zeus.
/// </summary>
public partial class ZeusBrain : Node2D
{
	[Export] public float MaxHealth { get; set; } = 220f;
	[Export] public float BoltDamage { get; set; } = 12f;
	[Export] public float SmiteDamage { get; set; } = 24f;
	[Export] public float BoltCooldownSeconds { get; set; } = 2.0f;
	[Export] public float SmiteCooldownSeconds { get; set; } = 5.0f;
	[Export] public float AreaDenialCooldownSeconds { get; set; } = 7.0f;

	private float _health;
	private bool _dead;
	private int _phase = 1;
	private float _boltCd;
	private float _smiteCd;
	private float _denialCd;
	private float _puzzleFailPunishLeft;
	private BehaviorTreeRunner _tree;
	private readonly BTBlackboard _blackboard = new();

	public override void _Ready()
	{
		_health = MaxHealth;
		BuildTree();

		if (PuzzleManager.Instance != null)
			PuzzleManager.Instance.TrialFailed += OnTrialFailed;
	}

	public override void _ExitTree()
	{
		if (PuzzleManager.Instance != null)
			PuzzleManager.Instance.TrialFailed -= OnTrialFailed;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_boltCd = Mathf.Max(0f, _boltCd - dt);
		_smiteCd = Mathf.Max(0f, _smiteCd - dt);
		_denialCd = Mathf.Max(0f, _denialCd - dt);
		_puzzleFailPunishLeft = Mathf.Max(0f, _puzzleFailPunishLeft - dt);
		UpdatePhaseAndBlackboard();
		_tree.Tick(this, delta);
	}

	public void TakeDamage(float amount)
	{
		if (_dead || amount <= 0f)
			return;
		_health = Mathf.Max(0f, _health - amount);
		if (_health <= 0f)
			_dead = true;
	}

	private void OnTrialFailed(string shrineId)
	{
		_puzzleFailPunishLeft = 4f;
	}

	private void BuildTree()
	{
		_tree = new BehaviorTreeRunner(
			new SelectorNode(
				new SequenceNode(
					new ConditionNode((o, bb, d) => _dead),
					new ActionNode((o, bb, d) =>
					{
						UnlockEscapeFlow();
						SetPhysicsProcess(false);
						return BTState.Success;
					})
				),
				new SequenceNode(
					new ConditionNode((o, bb, d) => _phase == 3),
					new SelectorNode(
						new SequenceNode(
							new ConditionNode((o, bb, d) => _smiteCd <= 0f),
							new ActionNode((o, bb, d) => CastSmiteCombo())
						),
						new SequenceNode(
							new ConditionNode((o, bb, d) => _puzzleFailPunishLeft > 0f),
							new ActionNode((o, bb, d) => CastPunishmentBolt())
						),
						new ActionNode((o, bb, d) => RepositionSkyAnchor())
					)
				),
				new SequenceNode(
					new ConditionNode((o, bb, d) => _phase == 2),
					new SelectorNode(
						new SequenceNode(
							new ConditionNode((o, bb, d) => _denialCd <= 0f),
							new ActionNode((o, bb, d) => SpawnAcidRainZone())
						),
						new SequenceNode(
							new ConditionNode((o, bb, d) => _boltCd <= 0f),
							new ActionNode((o, bb, d) => CastSingleBolt(BoltDamage))
						),
						new ActionNode((o, bb, d) => IdleHover())
					)
				),
				new SequenceNode(
					new ConditionNode((o, bb, d) => _phase == 1),
					new SelectorNode(
						new SequenceNode(
							new ConditionNode((o, bb, d) => _boltCd <= 0f),
							new ActionNode((o, bb, d) => CastSingleBolt(BoltDamage))
						),
						new ActionNode((o, bb, d) => Taunt())
					)
				),
				new ActionNode((o, bb, d) => IdleHover())
			),
			_blackboard
		);
	}

	private void UpdatePhaseAndBlackboard()
	{
		float hpPct = MaxHealth <= 0f ? 0f : (_health / MaxHealth);
		if (hpPct <= 0.33f)
			_phase = 3;
		else if (hpPct <= 0.66f)
			_phase = 2;
		else
			_phase = 1;

		_blackboard.Set("phase", _phase);
		_blackboard.Set("puzzle_fail_recent", _puzzleFailPunishLeft > 0f);
	}

	private BTState CastSingleBolt(float damage)
	{
		PlayerController player = PlayerController.Instance;
		if (player == null)
			return BTState.Failure;

		player.TakeDamage(damage);
		_boltCd = BoltCooldownSeconds;
		return BTState.Success;
	}

	private BTState CastPunishmentBolt()
	{
		_puzzleFailPunishLeft = 0f;
		return CastSingleBolt(SmiteDamage);
	}

	private BTState CastSmiteCombo()
	{
		PlayerController player = PlayerController.Instance;
		if (player == null)
			return BTState.Failure;

		player.TakeDamage(SmiteDamage);
		SpawnStormHazardNear(player.GlobalPosition + new Vector2(60f, 0f), 70f, 10f, 8f, 3.5f);
		SpawnStormHazardNear(player.GlobalPosition + new Vector2(-50f, 20f), 65f, 9f, 8f, 3.5f);
		SpawnStormHazardNear(player.GlobalPosition + new Vector2(15f, -45f), 55f, 12f, 7f, 3.5f);
		_smiteCd = SmiteCooldownSeconds;
		return BTState.Success;
	}

	private BTState SpawnAcidRainZone()
	{
		PlayerController player = PlayerController.Instance;
		if (player == null)
			return BTState.Failure;

		SpawnStormHazardNear(player.GlobalPosition, 95f, 12f, 10f, 5f);
		_denialCd = AreaDenialCooldownSeconds;
		return BTState.Success;
	}

	private BTState RepositionSkyAnchor()
	{
		PlayerController player = PlayerController.Instance;
		if (player == null)
			return BTState.Failure;

		Vector2 target = player.GlobalPosition + new Vector2(0f, -180f);
		GlobalPosition = GlobalPosition.Lerp(target, 0.04f);
		return BTState.Running;
	}

	private BTState Taunt()
	{
		// Placeholder hook for DialogueManager zeus lines.
		return BTState.Success;
	}

	private BTState IdleHover()
	{
		// Minimal idle to keep BT deterministic while waiting for cooldowns.
		return BTState.Running;
	}

	private void UnlockEscapeFlow()
	{
		// Hook for boss-fight mode; in current project, PuzzleManager still controls normal pod unlock.
	}

	private void SpawnStormHazardNear(Vector2 position, float radius, float o2Drain, float hpDrain, float lifeSeconds)
	{
		HazardZone hazard = new HazardZone
		{
			Position = position,
			O2DrainPerSecond = o2Drain,
			HpDrainPerSecond = hpDrain
		};

		var shape = new CollisionShape2D
		{
			Shape = new CircleShape2D { Radius = radius }
		};
		hazard.AddChild(shape);
		AddChild(hazard);

		var timer = new Timer
		{
			WaitTime = lifeSeconds,
			OneShot = true,
			Autostart = true
		};
		hazard.AddChild(timer);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(hazard))
				hazard.QueueFree();
		};
	}
}
