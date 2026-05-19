using Godot;
using System.Collections.Generic;

public partial class ZeusBrain
{
    // // Call this at start of every BTState attack method
    // private bool IsOnCastDelay() => _castDelayCd > 0f;

    private void StartCastDelay()
    {
        _castDelayCd = (float)GD.RandRange(MinCastDelay, MaxCastDelay);
        var t = GetTree().CreateTimer(_castDelayCd);
        t.Timeout += () =>
        {
            _castsThisRound = 0; // ← reset quota after rest
            GD.Print("[Zeus] Rested — ready to cast again");
        };
    }
    private BTState CastRoar()
    {
        if (!CanCast()) return BTState.Failure;
        _isStunned = true;
        var player = ResolvePlayer();
        if (player != null)
        {
            Vector2 dir = (player.GlobalPosition - GlobalPosition).Normalized();
            player.ApplyKnockback(dir * RoarKnockbackForce);
            player.SetStunned(RoarStunDuration);
        }
        SpawnRoarVfx();
        CameraController.Instance?.Shake(0.6f, 18f);
        _roarCd = RoarCooldownSeconds;
        var t = GetTree().CreateTimer(RoarStunDuration);
        t.Timeout += () => _isStunned = false;
        GD.Print("[Zeus] ROAR!");
        RegisterCast();
        return BTState.Success;
    }

    private BTState SpawnOrbRing()
    {
        if (!CanCast()) return BTState.Failure;
        _isCasting = true;
        int minOrbs = _phase == 1 ? 4 : _phase == 2 ? 6 : 8;
        int maxOrbs = _phase == 1 ? 8 : _phase == 2 ? 12 : 15;
        int orbCount = (int)GD.RandRange(minOrbs, maxOrbs);
        int burstCount = _phase == 1 ? 1 : (int)GD.RandRange(1, _phase == 2 ? 2 : 3);
        float speed = _phase == 1 ? OrbSpeed : _phase == 2 ? OrbSpeed * 1.4f : OrbSpeed * 1.8f;

        for (int burst = 0; burst < burstCount; burst++)
        {
            int b = burst;
            var delayTimer = GetTree().CreateTimer(burst * 0.4f);
            delayTimer.Timeout += () =>
            {
                OrbMovementType moveType = (OrbMovementType)(int)GD.RandRange(0, 3);
                float rotOffset = b * (Mathf.Tau / (orbCount * 2f));
                for (int i = 0; i < orbCount; i++)
                {
                    float angle = (Mathf.Tau / orbCount) * i + rotOffset;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    SpawnOrb(GlobalPosition, dir * speed, moveType);
                }
                GD.Print($"[Zeus] Burst {b + 1}/{burstCount} — {orbCount} orbs — {moveType}");
            };
        }

        float castDuration = burstCount * 0.4f + 0.5f;
        GetTree().CreateTimer(castDuration).Timeout += () => _isCasting = false;
        _orbCd = OrbCooldownSeconds;
        RegisterCast();
        return BTState.Success;
    }

    private BTState CastLightningSpear()
    {
        if (!CanCast()) return BTState.Failure;
        var player = ResolvePlayer();
        if (player == null) return BTState.Failure;
        _isCasting = true;

        int spearCount = _phase == 1 ? 10 : _phase == 2 ? 6 : 9;
        float spreadWidth = 300f;

        SpearPattern pattern = _phase == 1
            ? (SpearPattern)(int)GD.RandRange(0, 1)
            : (SpearPattern)(int)GD.RandRange(0, 2);

        var positions = new List<Vector2>();
        for (int i = 0; i < spearCount; i++)
        {
            float offsetX = spearCount == 1 ? 0f
                : Mathf.Lerp(-spreadWidth, spreadWidth, (float)i / (spearCount - 1));
            offsetX += (float)GD.RandRange(-20.0, 20.0);
            positions.Add(new Vector2(player.GlobalPosition.X + offsetX, player.GlobalPosition.Y));
        }

        float totalDuration = pattern switch
        {
            SpearPattern.OneByOne => spearCount * 0.3f + SpearWarningDuration + 0.5f,
            SpearPattern.AllAtOnce => SpearWarningDuration + 0.5f,
            SpearPattern.Batched => Mathf.CeilToInt((float)spearCount / (spearCount <= 4 ? 2 : 3))
                                      * (SpearWarningDuration + 0.3f) + 0.5f,
            _ => SpearWarningDuration + 1f
        };

        switch (pattern)
        {
            case SpearPattern.OneByOne: LaunchOneByOne(positions); break;
            case SpearPattern.AllAtOnce: LaunchAllAtOnce(positions); break;
            case SpearPattern.Batched: LaunchBatched(positions); break;
        }

        GetTree().CreateTimer(totalDuration).Timeout += () =>
        {
            _isCasting = false;
            GD.Print("[Zeus] Spear cast complete");
        };

        _spearCd = SpearCooldownSeconds;
        RegisterCast();
        return BTState.Success;
    }

    private void LaunchOneByOne(List<Vector2> positions)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            int captured = i;
            GetTree().CreateTimer(i * 0.3f).Timeout += () => SpawnFallingSpear(positions[captured]);
        }
    }

    private void LaunchAllAtOnce(List<Vector2> positions)
    {
        foreach (var pos in positions) SpawnFallingSpear(pos);
    }

    private void LaunchBatched(List<Vector2> positions)
    {
        int batchSize = positions.Count <= 4 ? 2 : 3;
        int batchCount = Mathf.CeilToInt((float)positions.Count / batchSize);

        for (int b = 0; b < batchCount; b++)
        {
            int start = b * batchSize;
            int end = Mathf.Min(start + batchSize, positions.Count);
            float delay = b * (SpearWarningDuration + 0.3f);

            for (int i = start; i < end; i++)
            {
                int captured = i;
                GetTree().CreateTimer(delay).Timeout += () => SpawnFallingSpear(positions[captured]);
            }
        }
    }

    private BTState SpawnAcidRainZone()
    {
        if (!CanCast()) return BTState.Failure;
        var player = ResolvePlayer();
        if (player == null) return BTState.Failure;
        _isCasting = true;
        _denialCd = AreaDenialCooldownSeconds;

        Vector2 spawnPos = player.GlobalPosition;

        SpawnAcidWarning(spawnPos, 95f, 1.0f, () =>
        {
            SpawnAcidHazard(spawnPos, 95f, 12f, 10f, 5f);
            _isCasting = false;
        });

        GD.Print("[Zeus] Acid rain incoming!");
        RegisterCast();
        return BTState.Success;
    }

    private BTState CastSingleBolt(float damage)
    {
        if (!CanCast()) return BTState.Failure;
        var player = ResolvePlayer();
        if (player == null) return BTState.Failure;
        PlayerStatManager.Instance?.TakeDamage(damage);
        SpawnZapVfx(player.GlobalPosition);
        _boltCd = BoltCooldownSeconds;
        RegisterCast();
        return BTState.Success;
    }

    private BTState CastPunishmentBolt()
    {
        if (!CanCast()) return BTState.Failure;
        _puzzleFailPunishLeft = 0f;
        return CastSingleBolt(SmiteDamage);
    }

    private BTState CastSmiteCombo()
    {
        if (!CanCast()) return BTState.Failure;
        var player = ResolvePlayer();
        if (player == null) return BTState.Failure;
        PlayerStatManager.Instance?.TakeDamage(SmiteDamage);
        SpawnZapVfx(player.GlobalPosition);
        SpawnStormHazardNear(player.GlobalPosition + new Vector2(60f, 0f), 70f, 10f, 8f, 3.5f);
        SpawnStormHazardNear(player.GlobalPosition + new Vector2(-50f, 20f), 65f, 9f, 8f, 3.5f);
        SpawnStormHazardNear(player.GlobalPosition + new Vector2(15f, -45f), 55f, 12f, 7f, 3.5f);
        _smiteCd = SmiteCooldownSeconds;
        RegisterCast();
        return BTState.Success;
    }

    private BTState RepositionSkyAnchor()
    {
        var player = ResolvePlayer();
        if (player == null) return BTState.Failure;
        Vector2 target = player.GlobalPosition + new Vector2(0f, -180f);
        GlobalPosition = GlobalPosition.Lerp(target, 0.04f);
        return BTState.Running;
    }

    private BTState Teleport()
    {
        if (_teleportCd > 0f) return BTState.Failure;

        // Pick random position within arena bounds
        Vector2 arenaCenter = _blackboard.Get<Vector2>("arena_center");
        Vector2 arenaSize = _blackboard.Get<Vector2>("arena_size");
        Vector2 half = arenaSize * 0.5f;

        Vector2 randomPos = new Vector2(
            (float)GD.RandRange(arenaCenter.X - half.X, arenaCenter.X + half.X),
            (float)GD.RandRange(arenaCenter.Y - half.Y, arenaCenter.Y + half.Y)
        );

        // Keep distance from player so it's not instant kill
        var player = ResolvePlayer();
        if (player != null)
        {
            int attempts = 0;
            while (randomPos.DistanceTo(player.GlobalPosition) < PreferredDistance * 0.5f
                   && attempts < 10)
            {
                randomPos = new Vector2(
                    (float)GD.RandRange(arenaCenter.X - half.X, arenaCenter.X + half.X),
                    (float)GD.RandRange(arenaCenter.Y - half.Y, arenaCenter.Y + half.Y)
                );
                attempts++;
            }
        }

        SpawnTeleportVfx(zeusBody.GlobalPosition); // old pos
        zeusBody.GlobalPosition = randomPos;
        SpawnTeleportVfx(zeusBody.GlobalPosition); // vfx at new position

        _teleportCd = TeleportCooldown;
        GD.Print($"[Zeus] Teleported to {randomPos}");
        return BTState.Success;
    }
}