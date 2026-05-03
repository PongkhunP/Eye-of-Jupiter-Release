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

	[Export] public float MaxO2 { get; set; } = 100f;

	[Export] public float MaxHp { get; set; } = 100f;

	/// <summary>Continuous Jupiter atmosphere O2 drain (units per second).</summary>
	[Export] public float O2DrainPerSecond { get; set; } = 2f;

	public float O2 { get; private set; }

	public float Hp { get; private set; }

	[Signal]
	public delegate void O2ChangedEventHandler(float current, float max);

	[Signal]
	public delegate void HpChangedEventHandler(float current, float max);

	[Signal]
	public delegate void PlayerDiedEventHandler();

	[Signal]
	public delegate void InteractPressedEventHandler();

	private Area2D _hazardDetector;
	private Area2D _interactRange;

	private bool _dead;
	private float _lastEmittedO2 = float.NaN;
	private float _lastEmittedHp = float.NaN;

	public override void _EnterTree()
	{
		Instance = this;
	}

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

		O2 = MaxO2;
		Hp = MaxHp;
		_hazardDetector = GetNodeOrNull<Area2D>("HazardDetector");
		_interactRange = GetNodeOrNull<Area2D>("InteractRange");

		EmitO2IfChanged();
		EmitHpIfChanged();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_dead)
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

		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() > 1f)
			input = input.Normalized();

		Velocity = input * Speed;
		MoveAndSlide();

		ApplyBreathingAndHazards(dt);
		TryInteract();
	}

	private void ApplyBreathingAndHazards(float dt)
	{
		float o2Drain = O2DrainPerSecond * dt;
		float hpDrain = 0f;

		if (_hazardDetector != null)
		{
			foreach (var area in _hazardDetector.GetOverlappingAreas())
			{
				if (area is HazardZone hz)
				{
					o2Drain += hz.O2DrainPerSecond * dt;
					hpDrain += hz.HpDrainPerSecond * dt;
				}
			}
		}

		if (o2Drain > 0f)
			AddO2(-o2Drain);

		if (hpDrain > 0f)
			TakeDamage(hpDrain);

		if (!_dead && O2 <= 0f)
			Die();
	}

	private void AddO2(float delta)
	{
		O2 = Mathf.Clamp(O2 + delta, 0f, MaxO2);
		EmitO2IfChanged();

		if (!_dead && O2 <= 0f)
			Die();
	}

	public void TakeDamage(float amount)
	{
		if (_dead || amount <= 0f)
			return;

		Hp = Mathf.Clamp(Hp - amount, 0f, MaxHp);
		EmitHpIfChanged();

		if (Hp <= 0f)
			Die();
	}

	private void Die()
	{
		if (_dead)
			return;

		_dead = true;
		Velocity = Vector2.Zero;
		EmitSignal(SignalName.PlayerDied);
	}

	private void EmitO2IfChanged()
	{
		if (float.IsNaN(_lastEmittedO2) || Mathf.Abs(_lastEmittedO2 - O2) > 0.001f)
		{
			_lastEmittedO2 = O2;
			EmitSignal(SignalName.O2Changed, O2, MaxO2);
		}
	}

	private void EmitHpIfChanged()
	{
		if (float.IsNaN(_lastEmittedHp) || Mathf.Abs(_lastEmittedHp - Hp) > 0.001f)
		{
			_lastEmittedHp = Hp;
			EmitSignal(SignalName.HpChanged, Hp, MaxHp);
		}
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
				break;
			}
		}

		foreach (Node2D body in _interactRange.GetOverlappingBodies())
		{
			if (body is IInteractable interactable)
			{
				interactable.Interact(this);
				break;
			}
		}
	}

	/// <summary>Refill oxygen (pickups, pod prep, etc.).</summary>
	public void RestoreO2(float amount)
	{
		if (_dead || amount <= 0f)
			return;

		AddO2(amount);
	}

	public void Heal(float amount)
	{
		if (_dead || amount <= 0f)
			return;

		Hp = Mathf.Clamp(Hp + amount, 0f, MaxHp);
		EmitHpIfChanged();
	}
}
