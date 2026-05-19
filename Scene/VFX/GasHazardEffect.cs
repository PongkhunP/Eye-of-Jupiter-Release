using Godot;
using System;

public partial class GasHazardEffect : AnimatedSprite2D
{
	[Export] public float MinDelay { get; set; } = 0f;
    [Export] public float MaxDelay { get; set; } = 3f;
    [Export] public float O2Drain  { get; set; } = 0.8f;
    [Export] public float HpDrain  { get; set; } = 0.5f;

    private HazardZone _hazard;

    public override void _Ready()
    {
        _hazard = GetNode<HazardZone>("HazardZone");

        // Disable collision until animation starts
        _hazard.SetDeferred("monitorable", false);

        float delay = (float)GD.RandRange(MinDelay, MaxDelay);

        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += StartHazard;
    }

    private void StartHazard()
    {
        Play("Play");
        _hazard.O2DrainPerSecond = O2Drain;
        _hazard.HpDrainPerSecond = HpDrain;
        _hazard.SetDeferred("monitorable", true); // enable damage only when playing
    }
}
