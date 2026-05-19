using Godot;

public partial class ZeusBrain
{
    private void UpdatePhaseAndBlackboard()
    {
        // GD.Print($"Phase : {_phase}");

        var player = ResolvePlayer();
        Vector2 playerPos = player?.GlobalPosition ?? GlobalPosition;
        float playerDist = GlobalPosition.DistanceTo(playerPos);

        bool puzzleActive = PuzzleManager.Instance.IsPuzzleActive;

        _blackboard.Set("phase", _phase);
        _blackboard.Set("player_position", playerPos);
        _blackboard.Set("preferred_distance", PreferredDistance);
        _blackboard.Set("player_distance", playerDist);
        _blackboard.Set("player_in_range", playerDist < DetectionRadius);
        _blackboard.Set("player_close", _playerInRange);
        _blackboard.Set("is_casting", _isCasting || puzzleActive);
        _blackboard.Set("is_stunned", _isStunned || puzzleActive);
        _blackboard.Set("is_vulnerable", _isCasting && _playerInRange && !puzzleActive);
        _blackboard.Set("puzzle_fail_recent", _puzzleFailPunishLeft > 0f);
        if (zeusBodyController.ArenaCenter != null)
        {
            _blackboard.Set("arena_center", zeusBodyController.ArenaCenter.GlobalPosition);
            _blackboard.Set("arena_size", zeusBodyController.ArenaSize);
        }

        if (player != null && !_isCasting && !_isStunned && !puzzleActive)
        {
            Vector2 awayDir = (GlobalPosition - playerPos).Normalized();
            Vector2 targetPos = playerPos + awayDir * PreferredDistance;
            _blackboard.Set("target_position", targetPos);
        }
    }

    private void OnRiddleCorrect(string shrineId)
    {
        if (shrineId != ZeusRiddleShrineId) return;

        AdvancePhase();

        // Unregister shrine so player can trigger again
        // but riddle ID stays in _usedRiddleIds so it won't repeat
        PuzzleManager.Instance.UnregisterTrial(ZeusRiddleShrineId);

        GD.Print("[Zeus] Shrine reset — riddle marked used, won't repeat");
    }

    private void OnRiddleWrong(string shrineId)
    {
        if (shrineId != ZeusRiddleShrineId) return;
        GD.Print("[Zeus] Wrong answer!");
        var player = ResolvePlayer();
        if (player == null) return;
        player.SetStunned(2.5f);
        CastSingleBolt(SmiteDamage);
    }

    private void OnTrialFailed(string shrineId) => _puzzleFailPunishLeft = 4f;
    private void UnlockEscapeFlow() { }
}