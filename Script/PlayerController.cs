using Godot;
using System;

/// <summary>
/// Top-down astronaut controller: O2 drain, HP, hazard overlap, interact (E).
/// Uses physics layers: Player=1, Hazard=2, Interactable=3 (see project Layer Names).
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	public static PlayerController Instance { get; private set; }

	[Export] public float Speed { get; set; } = 220f;

	[Signal] public delegate void InteractPressedEventHandler();

	private Area2D _hazardDetector;
	private Area2D _interactRange;

	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		MotionMode = MotionModeEnum.Floating;
		CollisionLayer = 1;
		CollisionMask = 0;

		_hazardDetector = GetNodeOrNull<Area2D>("HazardDetector");
		_interactRange = GetNodeOrNull<Area2D>("InteractRange");
	}

	// ── Physics loop ──────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		var stats = PlayerStatManager.Instance;
		if (stats == null)
		{
			GD.PrintErr("PlayerStatManager instance is null!");
			return;
		}

		if (stats.IsDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		var dm = DialogueManager.Instance;
		if (dm != null && dm.IsDialogueActive)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		HandleMovement();
		TickStats((float)delta, stats);
		TryInteract();
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	private void HandleMovement()
	{
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() > 1f)
			input = input.Normalized();
		Velocity = input * Speed;
		MoveAndSlide();
	}

	private void TickStats(float dt, PlayerStatManager stats)
	{
		float extraO2Drain = 0f;
		float extraHpDrain = 0f;

		if (_hazardDetector != null)
		{
			foreach (var area in _hazardDetector.GetOverlappingAreas())
			{
				if (area is HazardZone hz)
				{
					extraO2Drain += hz.O2DrainPerSecond;
					extraHpDrain += hz.HpDrainPerSecond;
				}
			}
		}

		stats.Tick(dt, extraO2Drain, extraHpDrain);
	}

	private void TryInteract()
	{
		if (!Input.IsActionJustPressed("interact"))
			return;

		EmitSignal(SignalName.InteractPressed);

		if (_interactRange == null)
			return;

		foreach (var area in _interactRange.GetOverlappingAreas())
		{
			if (area is IInteractable interactable)
			{
				interactable.Interact(this);
				return;
			}
		}

		foreach (Node2D body in _interactRange.GetOverlappingBodies())
		{
			if (body is IInteractable interactable)
			{
				interactable.Interact(this);
				return;
			}
		}
	}
}
