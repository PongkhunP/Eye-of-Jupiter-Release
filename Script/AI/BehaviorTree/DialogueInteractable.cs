using Godot;
using System;

public partial class DialogueInteractable : Node2D
{
	[Export] public string NpcId { get; set; } = "hermes";

	protected bool PlayerInRange = false;

	public override void _Ready()
	{
		Area2D area = null;
		foreach (var child in GetChildren())
		{
			if (child is Area2D a) { area = a; break; }
		}

		if (area == null)
		{
			GD.PrintErr($"{Name}: No Area2D child found!");
			return;
		}

		area.BodyEntered += OnBodyEntered;
		area.BodyExited += OnBodyExited;
		OnReady();
	}

	protected virtual void OnReady() { }

	public override void _Process(double delta)
	{
		if (!PlayerInRange) return;
		if (!Input.IsActionJustPressed("interact")) return;
		if (DialogueManager.Instance.IsDialogueActive) return;

		OnInteract();
	}

	protected virtual void OnInteract()
	{
		DialogueManager.Instance.StartDialogue(NpcId);
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
