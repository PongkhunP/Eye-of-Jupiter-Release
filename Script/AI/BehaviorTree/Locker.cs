using Godot;
using System;

public partial class Locker : Area2D
{
	[Export] public string NpcId { get; set; } = "hermes";

	private bool _playerInRange = false;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		GD.Print($"{Name} locker ready");
	}

	public override void _Process(double delta)
	{
		if (!_playerInRange) return;
		if (!Input.IsActionJustPressed("interact")) return;
		if (DialogueManager.Instance.IsDialogueActive) return;

		DialogueManager.Instance.StartDialogue(NpcId);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player")) _playerInRange = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player")) _playerInRange = false;
	}
}
