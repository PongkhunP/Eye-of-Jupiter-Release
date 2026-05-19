using Godot;
using System;

public partial class PuzzleNPC : PuzzleInteractable
{
    protected override void OnReady()
    {
        GD.Print($"{Name} ready — ShrineId: {ShrineId}, Type: {PuzzleKind}");
    }

    protected override void OnInteract()
    {
        if (PuzzleManager.Instance.IsShrineSolved(ShrineId))
        {
            GD.Print($"{Name}: already solved, skipping.");
            return;
        }
        StartPuzzle();
    }
}
