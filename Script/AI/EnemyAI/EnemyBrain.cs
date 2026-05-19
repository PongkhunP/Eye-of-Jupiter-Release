using Godot;
using System;

/// <summary>
/// Sprint 2 enemy behavior tree: patrol, chase, strike, investigate.
/// Attach to a CharacterBody2D enemy.
/// </summary>
public partial class EnemyBrain : CharacterBody2D
{
	[Export] public float MoveSpeed            { get; set; } = 120f;
    [Export] public float DetectionRange       { get; set; } = 320f;
    [Export] public float LoseAggroRange       { get; set; } = 450f;
    [Export] public float AttackRange          { get; set; } = 70f;
    [Export] public float AttackCooldown       { get; set; } = 1.2f;
    [Export] public float AttackDamage         { get; set; } = 8f;
    [Export] public float KnockbackForce       { get; set; } = 200f;
    [Export] public float MaxHealth            { get; set; } = 40f;
    [Export] public float PatrolWaitTime       { get; set; } = 1.5f;
    [Export] public float InvestigateTime      { get; set; } = 3f;
    [Export] public Godot.Collections.Array<NodePath> PatrolPoints { get; set; } = new();

    // ── State ─────────────────────────────────────────────────────
    internal float _health;
    internal bool  _dead;
    internal float _cooldownLeft;
    internal float _patrolWaitLeft;
    internal float _investigateLeft;
    internal int   _patrolIndex;
    internal bool  _facingRight = true;

    internal BehaviorTreeRunner _tree;
    internal readonly BTBlackboard _blackboard = new();

    internal AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        CollisionLayer = 1;
        CollisionMask  = 1;
        _health = MaxHealth;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        BuildTree();
        PlayAnim("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;

        float dt = (float)delta;
        _cooldownLeft    = Mathf.Max(0f, _cooldownLeft    - dt);
        _patrolWaitLeft  = Mathf.Max(0f, _patrolWaitLeft  - dt);
        _investigateLeft = Mathf.Max(0f, _investigateLeft - dt);

        UpdateBlackboard();
        _tree.Tick(this, delta);
        MoveAndSlide();

        // Flip sprite based on velocity
        if (Velocity.X != 0 && _sprite != null)
        {
            _facingRight     = Velocity.X > 0;
            _sprite.FlipH    = !_facingRight;
        }
    }

    public void TakeDamage(float damage)
    {
        if (_dead || damage <= 0f) return;
        _health = Mathf.Max(0f, _health - damage);

        FlashHit();

        if (_health <= 0f)
            Die();
    }

    internal void PlayAnim(string anim)
    {
        if (_sprite == null) return;
        if (!_sprite.SpriteFrames.HasAnimation(anim)) return;
        if (_sprite.Animation == anim && _sprite.IsPlaying()) return;
        _sprite.Play(anim);
    }

    internal PlayerController ResolvePlayer()
    {
        if (PlayerController.Instance != null) return PlayerController.Instance;
        foreach (var node in GetTree().GetNodesInGroup("player"))
            if (node is PlayerController pc) return pc;
        return null;
    }
}
