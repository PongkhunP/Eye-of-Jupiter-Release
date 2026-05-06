using Godot;
using System;

/// <summary>
/// Sprint 2 enemy behavior tree: patrol, chase, strike, investigate.
/// Attach to a CharacterBody2D enemy.
/// </summary>
public partial class EnemyBrain : CharacterBody2D
{
	[Export] public float MoveSpeed { get; set; } = 120f;
	[Export] public float DetectionRange { get; set; } = 320f;
	[Export] public float AttackRange { get; set; } = 70f;
	[Export] public float AttackCooldownSeconds { get; set; } = 1.2f;
	[Export] public float AttackDamage { get; set; } = 8f;
	[Export] public float MaxHealth { get; set; } = 40f;
	[Export] public Godot.Collections.Array<NodePath> PatrolPoints { get; set; } = new();

	private float _health;
	private bool _dead;
	private float _cooldownLeft;
	private BehaviorTreeRunner _tree;
	private readonly BTBlackboard _blackboard = new();

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		_health = MaxHealth;
		BuildTree();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt);
		UpdateBlackboard();
		_tree.Tick(this, delta);
		MoveAndSlide();
	}

	public void TakeDamage(float damage)
	{
		if (_dead || damage <= 0f)
			return;
		_health = Mathf.Max(0f, _health - damage);
		if (_health <= 0f)
			_dead = true;
	}

	private void BuildTree()
	{
		_tree = new BehaviorTreeRunner(
			new SelectorNode(
				new SequenceNode(
					new ConditionNode((o, bb, d) => _dead),
					new ActionNode((o, bb, d) =>
					{
						Velocity = Vector2.Zero;
						SetPhysicsProcess(false);
						QueueFree();
						return BTState.Success;
					})
				),
				new SequenceNode(
					new ConditionNode((o, bb, d) => bb.GetOrDefault<bool>("can_see_player")),
					new SelectorNode(
						new SequenceNode(
							new ConditionNode((o, bb, d) => bb.GetOrDefault<float>("distance_to_player") <= AttackRange),
							new ConditionNode((o, bb, d) => _cooldownLeft <= 0f),
							new ActionNode((o, bb, d) => AttackPlayer())
						),
						new ActionNode((o, bb, d) => ChasePlayer((float)d))
					)
				),
				new SequenceNode(
					new ConditionNode((o, bb, d) => bb.TryGet<Vector2>("last_known_pos", out _)),
					new ActionNode((o, bb, d) => MoveToLastKnown((float)d))
				),
				new ActionNode((o, bb, d) => Patrol((float)d))
			),
			_blackboard
		);
	}

	private void UpdateBlackboard()
	{
		PlayerController player = ResolvePlayer();
		if (player == null)
		{
			_blackboard.Set("can_see_player", false);
			return;
		}

		float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
		bool canSee = dist <= DetectionRange;

		_blackboard.Set("distance_to_player", dist);
		_blackboard.Set("can_see_player", canSee);
		if (canSee)
			_blackboard.Set("last_known_pos", player.GlobalPosition);
	}

	private BTState AttackPlayer()
	{
		PlayerController player = ResolvePlayer();
		if (player == null)
			return BTState.Failure;

		var stats = PlayerStatManager.Instance;
		if (stats == null)
			return BTState.Failure;

		stats.TakeDamage(AttackDamage);
		_cooldownLeft = AttackCooldownSeconds;
		return BTState.Success;
	}

	private BTState ChasePlayer(float delta)
	{
		PlayerController player = ResolvePlayer();
		if (player == null)
			return BTState.Failure;

		Vector2 dir = (player.GlobalPosition - GlobalPosition).Normalized();
		Velocity = dir * MoveSpeed;
		return BTState.Running;
	}

	private PlayerController ResolvePlayer()
	{
		if (PlayerController.Instance != null)
			return PlayerController.Instance;

		var tree = GetTree();
		if (tree == null)
			return null;

		foreach (var node in tree.GetNodesInGroup("player"))
		{
			if (node is PlayerController pc)
				return pc;
		}

		return null;
	}

	private BTState MoveToLastKnown(float delta)
	{
		if (!_blackboard.TryGet<Vector2>("last_known_pos", out Vector2 target))
			return BTState.Failure;

		Vector2 to = target - GlobalPosition;
		if (to.Length() < 8f)
		{
			Velocity = Vector2.Zero;
			return BTState.Success;
		}

		Velocity = to.Normalized() * MoveSpeed;
		return BTState.Running;
	}

	private BTState Patrol(float delta)
	{
		if (PatrolPoints == null || PatrolPoints.Count == 0)
		{
			Velocity = Vector2.Zero;
			return BTState.Success;
		}

		int patrolIndex = _blackboard.GetOrDefault("patrol_idx", 0);
		patrolIndex = Mathf.Clamp(patrolIndex, 0, PatrolPoints.Count - 1);
		Node2D node = GetNodeOrNull<Node2D>(PatrolPoints[patrolIndex]);
		if (node == null)
			return BTState.Failure;

		Vector2 to = node.GlobalPosition - GlobalPosition;
		if (to.Length() < 10f)
		{
			patrolIndex = (patrolIndex + 1) % PatrolPoints.Count;
			_blackboard.Set("patrol_idx", patrolIndex);
			return BTState.Success;
		}

		Velocity = to.Normalized() * MoveSpeed;
		return BTState.Running;
	}
}
