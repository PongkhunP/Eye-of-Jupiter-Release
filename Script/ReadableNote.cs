using Godot;
using System;

public partial class ReadableNote : Area2D
{
	[Export(PropertyHint.MultilineText)]
    public string NoteContent { get; set; } = "Write your note here...";

    private bool _playerInRange = false;
    private Label _hintLabel;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;
        _hintLabel = GetNodeOrNull<Label>("HintLabel");
        if (_hintLabel != null) _hintLabel.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_playerInRange) return;
        if (!Input.IsActionJustPressed("interact")) return;

		GD.Print($"Interact with Note , with Note Instace : {NoteUI.Instance == null}");

        NoteUI.Instance?.ShowNote(NoteContent);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerInRange = true;
        if (_hintLabel != null) _hintLabel.Visible = true;
    }

    private void OnBodyExited(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerInRange = false;
        if (_hintLabel != null) _hintLabel.Visible = false;
    }
}
