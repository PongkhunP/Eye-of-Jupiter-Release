using Godot;
using System;

public partial class PuzzleInteractable : Node2D
{
	[Export] public string ShrineId { get; set; } = "shrine_a";
    [Export] public PuzzleType PuzzleKind { get; set; } = PuzzleType.Math;
    [Export] public float TimeOverride { get; set; } = 0f;

    protected bool PlayerInRange = false;

    public override void _Ready()
    {
        var area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
        area.BodyExited  += OnBodyExited;
        OnReady();
    }

    // Override in subclass for extra _Ready logic
    protected virtual void OnReady() { }

    public override void _Process(double delta)
    {
        if (!PlayerInRange) return;
        if (!Input.IsActionJustPressed("interact")) return;
        if (PuzzleManager.Instance.IsPuzzleActive) return;
        if (DialogueManager.Instance.IsDialogueActive) return;

        OnInteract();
    }

    // Subclass defines what happens on interact
    protected virtual void OnInteract() { }

    protected void StartPuzzle()
    {
        if (PuzzleManager.Instance.IsShrineSolved(ShrineId)) return;

        float? time = TimeOverride > 0f ? TimeOverride : null;

        switch (PuzzleKind)
        {
            case PuzzleType.Math:           PuzzleManager.Instance.StartMathPuzzle(ShrineId, time); break;
            case PuzzleType.NumberSequence: PuzzleManager.Instance.StartSequencePuzzle(ShrineId, time); break;
            case PuzzleType.ZeusRiddle:     PuzzleManager.Instance.StartRiddlePuzzle(ShrineId, time); break;
            case PuzzleType.MemoryPuzzle:   PuzzleManager.Instance.StartMemoryPuzzle(ShrineId, time); break;
            case PuzzleType.OpenTheLock:    PuzzleManager.Instance.StartOpenLockPuzzle(ShrineId, time); break;
            case PuzzleType.PipePuzzle:     PuzzleManager.Instance.StartPipePuzzle(ShrineId, 2, time); break;
            case PuzzleType.PlanRoute:      PuzzleManager.Instance.StartPlanRoutePuzzle(ShrineId); break;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("player")) PlayerInRange = true;
    }

    private void OnBodyExited(Node2D body)
    {
        if (body.IsInGroup("player")) PlayerInRange = false;
    }
}
