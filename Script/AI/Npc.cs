using Godot;
using System;

public partial class Npc : DialogueInteractable
{
    protected override void OnReady()
    {
        GD.Print($"{Name} ready with NPC ID: {NpcId}");
    }
}
