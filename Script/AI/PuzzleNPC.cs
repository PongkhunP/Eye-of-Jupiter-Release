using Godot;
using System;

public partial class PuzzleNPC : Node2D
{
	[Export] public string ShrineId   = "shrine_a";       // must be unique per shrine
    [Export] public PuzzleType PuzzleKind = PuzzleType.Math; // set in editor per NPC
    [Export] public float TimeOverride = 0f;

	private bool _playerInRange = false;
	 public override void _Ready()
    {
        var area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
        area.BodyExited  += OnBodyExited;
        GD.Print($"{Name} ready — ShrineId: {ShrineId}, Type: {PuzzleKind}");
    }

	public override void _Process(double delta)
    {
        if (!_playerInRange) return;
        if (!Input.IsActionJustPressed("interact")) return;

        // Don't interrupt an active puzzle or dialogue
        if (PuzzleManager.Instance.IsPuzzleActive) return;
        if (DialogueManager.Instance.IsDialogueActive) return;

        // Don't re-trigger a shrine the player already solved
        if (PuzzleManager.Instance.IsShrineSolved(ShrineId))
        {
            GD.Print($"{Name}: already solved, skipping.");
            return;
        }

        GD.Print($"Starting {PuzzleKind} puzzle for {ShrineId}");
        StartPuzzle();
    }

    private void StartPuzzle()
    {
        float? time = TimeOverride > 0f ? TimeOverride : null;

        switch (PuzzleKind)
        {
            case PuzzleType.Math:
                PuzzleManager.Instance.StartMathPuzzle(ShrineId, time);
                break;
            case PuzzleType.NumberSequence:
                PuzzleManager.Instance.StartSequencePuzzle(ShrineId, time);
                break;
            case PuzzleType.ZeusRiddle:
                PuzzleManager.Instance.StartRiddlePuzzle(ShrineId, time);
                break;
            case PuzzleType.MemoryPuzzle:
                PuzzleManager.Instance.StartMemoryPuzzle(ShrineId, time);
                break;
        }
    }

	private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("player"))
        {
            GD.Print($"Player entered {Name}'s area");
            _playerInRange = true;
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body.IsInGroup("player"))
        {
            GD.Print($"Player exited {Name}'s area");
            _playerInRange = false;
        }
    }
}
