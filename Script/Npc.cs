using Godot;
using System;

public partial class Npc : Node2D
{
	[Export] public string NpcId = "hermes"; // must match key in dialogue.json

    private bool _playerInRange = false;

    public override void _Ready()
    {
        // Connect the Area2D signals
        var area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
        area.BodyExited  += OnBodyExited;
		GD.Print($"{Name} ready with NPC ID: {NpcId}"); // Debug log to confirm NPC is ready
    }

    public override void _Process(double delta)
    {
        if (_playerInRange && Input.IsActionJustPressed("interact"))
        {
            if (!DialogueManager.Instance.IsDialogueActive)
			{
				GD.Print($"Starting dialogue with {Name}");
                DialogueManager.Instance.StartDialogue(NpcId);
			}
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
