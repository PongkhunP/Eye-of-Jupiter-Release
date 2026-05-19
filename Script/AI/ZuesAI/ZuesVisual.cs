using Godot;
using System;

public enum OrbMovementType
{
	Straight,    // direct outward — current behavior
	Spiral,      // curves outward in a spiral
	Sine,        // wiggles side to side while moving out
	Boomerang    // goes out then curves back toward Zeus
}

public enum SpearPattern
{
	OneByOne,   // staggered one at a time
	AllAtOnce,  // all fall simultaneously
	Batched     // fall in groups
}

/// <summary>Simple mover script for electric orbs.</summary>
public partial class OrbMover : Node
{
	public Vector2 Velocity { get; set; }
	public float Damage { get; set; } = 10f;
	public OrbMovementType MoveType { get; set; } = OrbMovementType.Straight;
	public Vector2 ZeusOrigin { get; set; }

	private float _age = 0f;
	private float _sinePhase = 0f;

	public override void _Ready()
	{
		if (GetParent() is Area2D area)
		{

			area.BodyEntered += OnBodyEntered;
			area.CollisionLayer = 4;  // hazard layer
        area.CollisionMask  = 4;
		}

		// Randomize sine phase so orbs don't all wiggle in sync
		_sinePhase = (float)GD.RandRange(0.0, Mathf.Tau);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GetParent() is not Node2D parent) return;

		float dt = (float)delta;
		_age += dt;

		Vector2 move = MoveType switch
		{
			OrbMovementType.Straight => Straight(dt),
			OrbMovementType.Spiral => Spiral(dt),
			OrbMovementType.Sine => Sine(dt),
			OrbMovementType.Boomerang => Boomerang(dt, parent),
			_ => Straight(dt)
		};

		parent.GlobalPosition += move;
	}

	// ── Movement types ────────────────────────────────────────────────────────

	private Vector2 Straight(float dt)
	{
		return Velocity * dt;
	}

	private Vector2 Spiral(float dt)
	{
		// Rotate velocity direction over time — creates outward spiral
		float rotSpeed = 1.8f; // radians per second
		Velocity = Velocity.Rotated(rotSpeed * dt);
		return Velocity * dt;
	}

	private Vector2 Sine(float dt)
	{
		// Move in base direction but wiggle perpendicular
		Vector2 baseDir = Velocity.Normalized();
		Vector2 perpDir = new Vector2(-baseDir.Y, baseDir.X);

		float wiggle = Mathf.Sin(_age * 3.5f + _sinePhase) * 60f;
		return (Velocity + perpDir * wiggle) * dt;
	}

	private Vector2 Boomerang(float dt, Node2D parent)
	{
		if (_age < 1.5f)
		{
			Velocity = Velocity.Lerp(Velocity * 0.98f, dt);
			return Velocity * dt;
		}

		// Use fixed return speed instead of Velocity.Length() which is near zero
		float returnSpeed = 150f;
		Vector2 toZeus = (ZeusOrigin - parent.GlobalPosition).Normalized();

		// Snap velocity toward Zeus direction immediately
		Velocity = Velocity.Lerp(toZeus * returnSpeed, dt * 5f);

		// GD.Print($"Age: {_age}, DistToZeus: {parent.GlobalPosition.DistanceTo(ZeusOrigin)}, Vel: {Velocity.Length()}");

		// Self-destroy when close enough
		if (parent.GlobalPosition.DistanceTo(ZeusOrigin) < 20f)
		{
			parent.QueueFree();
			return Vector2.Zero;
		}

		return Velocity * dt;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		PlayerStatManager.Instance?.TakeDamage(Damage);
		GD.Print($"Dealt {Damage} damage!");
		GetParent()?.QueueFree();
	}
}

public partial class OrbVisual : Node2D
{
	public Color OrbColor { get; set; } = Colors.Cyan;
	public float Radius { get; set; } = 10f;

	public override void _Draw()
	{
		// Glowing orb — outer glow + solid core
		DrawCircle(Vector2.Zero, Radius * 1.6f, new Color(OrbColor.R, OrbColor.G, OrbColor.B, 0.25f));
		DrawCircle(Vector2.Zero, Radius, OrbColor);
		DrawCircle(Vector2.Zero, Radius * 0.4f, Colors.White); // highlight
	}
}

public partial class SpearMover : Node
{
	public float FallSpeed { get; set; } = 400f;

	public override void _PhysicsProcess(double delta)
	{
		if (GetParent() is Node2D parent)
			parent.GlobalPosition += new Vector2(0f, FallSpeed * (float)delta);
	}
}

public partial class SpearVisual : Node2D
{
	public override void _Draw()
	{
		// Spear tip pointing up, body going down
		var points = new Vector2[]
		{
			new Vector2(  0f,   0f),  // tip
            new Vector2(  6f,  20f),  // right shoulder
            new Vector2(  4f,  70f),  // right body
            new Vector2( -4f,  70f),  // left body
            new Vector2( -6f,  20f),  // left shoulder
            new Vector2(  0f,   0f),  // back to tip
        };

		// Glow
		DrawColoredPolygon(points, new Color(0.9f, 0.9f, 1f, 0.3f));
		// Core
		DrawPolyline(points, new Color(1f, 1f, 1f, 0.95f), 2f, true);
		// Bright tip
		DrawCircle(Vector2.Zero, 4f, Colors.White);
	}
}

// Flashing warning circle before acid activates
public partial class AcidWarningVisual : Node2D
{
	public float Radius { get; set; } = 95f;
	private float _age = 0f;

	public override void _Process(double delta)
	{
		_age += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		// Flash by modulating alpha with sine wave
		float alpha = (Mathf.Sin(_age * 10f) + 1f) * 0.5f; // 0..1 flicker
		DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 64,
			new Color(0.8f, 0f, 0f, alpha), 3f);
		DrawCircle(Vector2.Zero, Radius,
			new Color(0.6f, 0f, 0f, alpha * 0.2f));
	}
}

// Persistent acid zone visual
public partial class AcidHazardVisual : Node2D
{
	public float Radius { get; set; } = 95f;
	private float _age = 0f;

	public override void _Process(double delta)
	{
		_age += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		// Pulsing dark red filled circle
		float pulse = (Mathf.Sin(_age * 3f) + 1f) * 0.5f; // slow pulse
		float alpha = Mathf.Lerp(0.3f, 0.55f, pulse);

		DrawCircle(Vector2.Zero, Radius,
			new Color(0.5f, 0f, 0f, alpha));
		DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 64,
			new Color(1f, 0.1f, 0.1f, 0.8f), 2f);

		// Inner bubbling effect — small random dots
		for (int i = 0; i < 6; i++)
		{
			float a = Mathf.Tau / 6f * i + _age;
			float dist = Radius * 0.5f;
			var dot = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
			DrawCircle(dot, 4f, new Color(0.8f, 0.1f, 0.1f, 0.6f));
		}
	}
}
