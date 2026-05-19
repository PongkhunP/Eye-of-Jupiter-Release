using Godot;

public partial class EnemyBrain
{
	private BTState AttackPlayer()
	{
		var player = ResolvePlayer();
		if (player == null) return BTState.Failure;

		Velocity = Vector2.Zero;
		PlayAnim("attack");

		// Apply damage + knockback
		PlayerStatManager.Instance?.TakeDamage(AttackDamage);

		Vector2 knockDir = (player.GlobalPosition - GlobalPosition).Normalized();
		player.ApplyKnockback(knockDir * KnockbackForce);
		player.SetStunned(0.3f);

		_cooldownLeft = AttackCooldown;

		// Return to idle after attack anim
		var t = GetTree().CreateTimer(0.4f);
		t.Timeout += () => PlayAnim("idle");

		return BTState.Success;
	}

	private BTState ChasePlayer(float delta)
	{
		var player = ResolvePlayer();
		if (player == null) return BTState.Failure;

		Vector2 dir = (player.GlobalPosition - GlobalPosition).Normalized();
		Velocity = dir * MoveSpeed;
		PlayAnim("run");
		return BTState.Running;
	}

	private BTState Investigate(float delta)
	{
		if (!_blackboard.TryGet<Vector2>("last_known_pos", out Vector2 target))
			return BTState.Failure;

		Vector2 to = target - GlobalPosition;

		if (to.Length() < 12f)
		{
			// Reached last known pos — look around then give up
			Velocity = Vector2.Zero;
			PlayAnim("idle");
			_investigateLeft -= delta;

			if (_investigateLeft <= 0f)
				_blackboard.Set("last_known_pos", GlobalPosition);

			return BTState.Running;
		}

		Velocity = to.Normalized() * (MoveSpeed * 0.7f); // slower when investigating
		PlayAnim("run");
		return BTState.Running;
	}

	private BTState Patrol(float delta)
	{
		// No patrol points — just idle
		if (PatrolPoints == null || PatrolPoints.Count == 0)
		{
			Velocity = Vector2.Zero;
			PlayAnim("idle");
			return BTState.Success;
		}

		// Waiting at patrol point
		if (_patrolWaitLeft > 0f)
		{
			Velocity = Vector2.Zero;
			PlayAnim("idle");
			return BTState.Running;
		}

		_patrolIndex = Mathf.Clamp(_patrolIndex, 0, PatrolPoints.Count - 1);
		var node = GetNodeOrNull<Node2D>(PatrolPoints[_patrolIndex]);
		if (node == null) return BTState.Failure;

		Vector2 to = node.GlobalPosition - GlobalPosition;

		if (to.Length() < 10f)
		{
			// Reached point — wait then move to next
			_patrolIndex = (_patrolIndex + 1) % PatrolPoints.Count;
			_patrolWaitLeft = PatrolWaitTime;
			return BTState.Running;
		}

		Velocity = to.Normalized() * MoveSpeed;
		PlayAnim("run");
		return BTState.Running;
	}

	private BTState HandleDeath()
	{
		Velocity = Vector2.Zero;
		SetPhysicsProcess(false);
		PlayAnim("death");

		// Wait for death anim then free
		float deathDuration = 0.8f;
		if (_sprite != null && _sprite.SpriteFrames.HasAnimation("death"))
		{
			int frameCount = _sprite.SpriteFrames.GetFrameCount("death");
			float animSpeed = (float)_sprite.SpriteFrames.GetAnimationSpeed("death"); 
			deathDuration = frameCount / animSpeed;
		}

		var t = GetTree().CreateTimer(deathDuration);
		t.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };

		return BTState.Success;
	}

	private void Die()
	{
		_dead = true;
		GD.Print($"[Enemy] {Name} died");
	}
}