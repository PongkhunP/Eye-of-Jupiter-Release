using Godot;

public partial class ZeusBrain
{
    private void SpawnOrb(Vector2 position, Vector2 velocity, OrbMovementType moveType = OrbMovementType.Straight)
    {
        var orb = new Area2D();
        var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 10f } };
        orb.CollisionLayer = 2;
        orb.CollisionMask = 3;
        orb.AddChild(shape);
        orb.GlobalPosition = position;
        orb.AddToGroup("zeus_orb");

        float lifetime = moveType == OrbMovementType.Boomerang ? OrbLifetime * 2.5f : OrbLifetime;

        var visual = new OrbVisual
        {
            OrbColor = moveType switch
            {
                OrbMovementType.Straight => new Color(0.3f, 0.8f, 1f),
                OrbMovementType.Spiral => new Color(0.8f, 0.3f, 1f),
                OrbMovementType.Sine => new Color(0.3f, 1f, 0.5f),
                OrbMovementType.Boomerang => new Color(1f, 0.5f, 0.1f),
                _ => Colors.Cyan
            },
            Radius = 10f
        };
        orb.AddChild(visual);

        var mover = new OrbMover
        {
            Velocity = velocity,
            Damage = OrbDamage,
            MoveType = moveType,
            ZeusOrigin = position
        };
        orb.AddChild(mover);

        GetTree().CurrentScene?.AddChild(orb);
        GetTree().CreateTimer(lifetime).Timeout += () => { if (IsInstanceValid(orb)) orb.QueueFree(); };
    }

    private void SpawnFallingSpear(Vector2 targetPos)
    {
        float spawnY = targetPos.Y - 600f;
        float warningX = targetPos.X;

        var warning = new Line2D
        {
            TopLevel = true,
            DefaultColor = new Color(1f, 0.15f, 0.15f, 0.6f),
            Width = 4f
        };
        warning.AddPoint(new Vector2(warningX, spawnY));
        warning.AddPoint(new Vector2(warningX, targetPos.Y + 400f));
        GetTree().CurrentScene?.AddChild(warning);

        StartFlicker(warning);

        GetTree().CreateTimer(SpearWarningDuration).Timeout += () =>
        {
            if (IsInstanceValid(warning)) warning.QueueFree();
            LaunchSpear(new Vector2(warningX, spawnY));
        };
    }

    private void StartFlicker(Line2D warning)
    {
        if (!IsInstanceValid(warning)) return;
        warning.Visible = !warning.Visible;
        GetTree().CreateTimer(0.1f).Timeout += () => StartFlicker(warning);
    }

    private void LaunchSpear(Vector2 spawnPos)
    {
        var spear = new Area2D
        {
            CollisionLayer = 4,
            CollisionMask = 4,
            GlobalPosition = spawnPos,
            TopLevel = true
        };

        spear.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(12f, 60f) },
            Position = new Vector2(0f, 30f)
        });
        spear.AddChild(new SpearVisual());

        spear.BodyEntered += (body) =>
        {
            if (!body.IsInGroup("player")) return;
            PlayerStatManager.Instance?.TakeDamage(SpearDamage);
            ResolvePlayer()?.SetStunned(0.5f);
            spear.QueueFree();
        };

        GetTree().CurrentScene?.AddChild(spear);
        spear.AddChild(new SpearMover { FallSpeed = 400f });
        GetTree().CreateTimer(3f).Timeout += () => { if (IsInstanceValid(spear)) spear.QueueFree(); };
    }

    private void SpawnAcidWarning(Vector2 pos, float radius, float duration, System.Action onComplete)
    {
        var warning = new AcidWarningVisual { GlobalPosition = pos, Radius = radius, TopLevel = true };
        GetTree().CurrentScene?.AddChild(warning);
        GetTree().CreateTimer(duration).Timeout += () =>
        {
            if (IsInstanceValid(warning)) warning.QueueFree();
            onComplete?.Invoke();
        };
    }

    private void SpawnAcidHazard(Vector2 pos, float radius, float o2Drain, float hpDrain, float lifetime)
    {
        var hazard = new HazardZone { O2DrainPerSecond = o2Drain, HpDrainPerSecond = hpDrain };
        hazard.GlobalPosition = pos;
        hazard.TopLevel = true;
        hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
        hazard.AddChild(new AcidHazardVisual { Radius = radius });
        GetTree().CurrentScene?.AddChild(hazard);
        GetTree().CreateTimer(lifetime).Timeout += () => { if (IsInstanceValid(hazard)) hazard.QueueFree(); };
    }

    private void SpawnStormHazardNear(Vector2 position, float radius, float o2Drain, float hpDrain, float lifeSeconds)
    {
        var hazard = new HazardZone { Position = position, O2DrainPerSecond = o2Drain, HpDrainPerSecond = hpDrain };
        hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
        AddChild(hazard);
        var timer = new Timer { WaitTime = lifeSeconds, OneShot = true, Autostart = true };
        hazard.AddChild(timer);
        timer.Timeout += () => { if (IsInstanceValid(hazard)) hazard.QueueFree(); };
    }

    private void SpawnRoarVfx()
    {
        for (int i = 0; i < 3; i++)
        {
            float delay = i * 0.15f;
            GetTree().CreateTimer(delay).Timeout += () =>
            {
                var ring = new Line2D { TopLevel = true, DefaultColor = new Color(0.9f, 0.8f, 0.1f, 0.7f), Width = 3f };
                for (int p = 0; p <= 32; p++)
                {
                    float a = Mathf.Tau / 32 * p;
                    ring.AddPoint(GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 60f);
                }
                GetTree().CurrentScene?.AddChild(ring);
                GetTree().CreateTimer(0.4f).Timeout += () => { if (IsInstanceValid(ring)) ring.QueueFree(); };
            };
        }
    }

    private void SpawnZapVfx(Vector2 targetPosition)
    {
        if (!ShowZapVfx) return;
        var line = new Line2D { TopLevel = true, ZIndex = 200, Width = ZapVfxWidth, DefaultColor = new Color(0.95f, 0.95f, 0.2f, 0.95f) };
        line.AddPoint(GlobalPosition);
        line.AddPoint(targetPosition);
        GetTree().CurrentScene?.AddChild(line);
        var timer = new Timer { OneShot = true, WaitTime = ZapVfxDurationSeconds, Autostart = true };
        line.AddChild(timer);
        timer.Timeout += () => { if (IsInstanceValid(line)) line.QueueFree(); };
    }

    private void SpawnTeleportVfx(Vector2 pos)
    {
        // Flash ring at teleport position
        for (int i = 0; i < 2; i++)
        {
            float delay = i * 0.1f;
            GetTree().CreateTimer(delay).Timeout += () =>
            {
                var ring = new Line2D
                {
                    TopLevel = true,
                    DefaultColor = new Color(0.6f, 0.2f, 1f, 0.8f), // purple
                    Width = 4f
                };
                for (int p = 0; p <= 32; p++)
                {
                    float a = Mathf.Tau / 32 * p;
                    ring.AddPoint(pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f);
                }
                GetTree().CurrentScene?.AddChild(ring);
                GetTree().CreateTimer(0.3f).Timeout += () =>
                {
                    if (IsInstanceValid(ring)) ring.QueueFree();
                };
            };
        }
    }
}