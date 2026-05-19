using Godot;
using System;

public partial class Doors : PuzzleInteractable
{
    [Export] public bool RequiresPuzzle { get; set; } = true;
    [Export] public string NextScenePath { get; set; } = "res://Scene/Map/central_hub.tscn";
    [Export] public float TransitionDuration { get; set; } = 0.5f;

    private Label _hintLabel;
    private GpuParticles2D _sparkle;

    protected override void OnReady()
    {
        _hintLabel = GetNodeOrNull<Label>("HintLabel");
        if (_hintLabel != null) _hintLabel.Visible = false;

        if (RequiresPuzzle)
            PuzzleManager.Instance.TrialCompleted += OnTrialComplete;
    }

    public override void _ExitTree()
    {
        if (RequiresPuzzle && PuzzleManager.Instance != null)
            PuzzleManager.Instance.TrialCompleted -= OnTrialComplete;
    }

    protected override void OnInteract()
    {
        if (!RequiresPuzzle)
        {
            SceneTransitionManager.Instance.TransitionToScene(NextScenePath, TransitionDuration);
            return;
        }

        if (!PuzzleManager.Instance.IsShrineSolved(ShrineId))
        {
            ShowHint("Solve the puzzle first!");
            StartPuzzle();
            return;
        }
		else
		{
			SceneTransitionManager.Instance.TransitionToScene(NextScenePath, TransitionDuration);
            return;
		}
    }

    private void ShowHint(string message)
    {
        if (_hintLabel == null) return;
        _hintLabel.Text = message;
        _hintLabel.Visible = true;
        var timer = GetTree().CreateTimer(2f);
        timer.Timeout += () => { if (IsInstanceValid(_hintLabel)) _hintLabel.Visible = false; };
    }

    private void OnTrialComplete(string shrineId)
    {
        if (shrineId != ShrineId) return;
        SceneTransitionManager.Instance.TransitionToScene(NextScenePath, TransitionDuration);
    }
}
