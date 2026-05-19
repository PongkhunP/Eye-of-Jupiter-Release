using Godot;
using System.Collections.Generic;

public partial class ObstacleManager : Node2D
{
    [Export] public TileMapLayer FloorLayer { get; set; }

    /// <summary>Empty TileMapLayer that hazard tiles get painted onto at runtime.</summary>
    [Export] public TileMapLayer HazardLayer { get; set; }

    /// <summary>Fraction of floor tiles that become hazard tiles (0.0 – 1.0).</summary>
    [Export] public float ScatterDensity { get; set; } = 0.2f;

    /// <summary>TileSet source ID of the hazard tile. Check your TileSet panel.</summary>
    [Export] public int HazardSourceId { get; set; } = 0;

    /// <summary>Atlas coordinates of the hazard tile in the TileSet.</summary>
    [Export] public Vector2I HazardAtlasCoords { get; set; } = new Vector2I(0, 0);

    // ── Exports: Hazard zone spawning ─────────────────────────────────────────

    /// <summary>One HazardZone is placed per NxN cluster of hazard tiles.</summary>
    [Export] public int ClusterSize { get; set; } = 3;

    /// <summary>Spawn hazard zones within this world-space distance from the player.</summary>
    [Export] public float SpawnRadius { get; set; } = 400f;

    /// <summary>Remove hazard zones beyond this distance from the player.</summary>
    [Export] public float DespawnRadius { get; set; } = 600f;

    /// <summary>Seconds between spawn/despawn checks. Lower = more responsive.</summary>
    [Export] public float CheckInterval { get; set; } = 1f;

    [Export] public float HazardO2Drain { get; set; } = 0.8f;
    [Export] public float HazardHpDrain { get; set; } = 0.5f;
    [Export] public PackedScene GasEffectScene { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    // Cluster origin cell → active HazardZone
    private readonly Dictionary<Vector2I, HazardZone> _spawnedZones = new();
    private float _timer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        if (FloorLayer == null || HazardLayer == null)
        {
            GD.PrintErr("ObstacleManager: FloorLayer or HazardLayer not assigned in Inspector.");
            return;
        }

        GenerateTiles();
    }

    public override void _PhysicsProcess(double delta)
    {
        _timer += (float)delta;
        if (_timer < CheckInterval)
            return;

        _timer = 0f;

        var player = PlayerController.Instance;
        if (player == null)
            return;

        UpdateHazards(player.GlobalPosition);
    }

    // ── Stage 1: Tile generation ──────────────────────────────────────────────

    private void GenerateTiles()
    {
        var baseCells = FloorLayer.GetUsedCells();

        if (baseCells.Count == 0)
        {
            GD.PrintErr("ObstacleManager: FloorLayer has no tiles to scatter hazards onto.");
            return;
        }

        // Shuffle all floor cells then paint the first ScatterDensity fraction
        var shuffled = new List<Vector2I>(baseCells);
        Shuffle(shuffled);

        int count = Mathf.Max(1, Mathf.RoundToInt(shuffled.Count * ScatterDensity));

        for (int i = 0; i < count; i++)
            HazardLayer.SetCell(shuffled[i], HazardSourceId, HazardAtlasCoords);

        GD.Print($"ObstacleManager: {baseCells.Count} floor tiles → {count} hazard tiles ({ScatterDensity * 100f:0}% density).");
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = (int)GD.RandRange(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Stage 2: Hazard zone management ───────────────────────────────────────

    private void UpdateHazards(Vector2 playerPos)
    {
        // Spawn zones for clusters now in range
        foreach (var (clusterOrigin, tiles) in GetClustersInRange(playerPos, SpawnRadius))
        {
            if (!_spawnedZones.ContainsKey(clusterOrigin))
                SpawnHazard(clusterOrigin, tiles);
        }

        // Despawn zones that left the range
        var toRemove = new List<Vector2I>();

        foreach (var (clusterOrigin, zone) in _spawnedZones)
        {
            if (!IsInstanceValid(zone) || zone.GlobalPosition.DistanceTo(playerPos) > DespawnRadius)
            {
                if (IsInstanceValid(zone))
                    zone.QueueFree();

                toRemove.Add(clusterOrigin);
            }
        }

        foreach (var cell in toRemove)
            _spawnedZones.Remove(cell);
    }

    private void SpawnHazard(Vector2I clusterOrigin, List<Vector2I> tiles)
    {
        if (tiles.Count == 0 || GasEffectScene == null) return;

        Vector2 tileSize = HazardLayer.TileSet.TileSize;
        Vector2I randomTile = tiles[GD.RandRange(0, tiles.Count - 1)];
        Vector2 spawnPos = HazardLayer.MapToLocal(randomTile);

        var effect = GasEffectScene.Instantiate<GasHazardEffect>();
        effect.O2Drain = HazardO2Drain;
        effect.HpDrain = HazardHpDrain;
        effect.GlobalPosition = spawnPos;
        AddChild(effect);

        // Store root node for despawn tracking
        _spawnedZones[clusterOrigin] = effect.GetNode<HazardZone>("HazardZone");
    }

    // ── Tile scanning ─────────────────────────────────────────────────────────

    private Dictionary<Vector2I, List<Vector2I>> GetClustersInRange(Vector2 playerPos, float radius)
    {
        var clusters = new Dictionary<Vector2I, List<Vector2I>>();
        Vector2 tileSize = HazardLayer.TileSet.TileSize;

        Vector2I centerCell = HazardLayer.LocalToMap(playerPos);
        int cellRadius = Mathf.CeilToInt(radius / Mathf.Min(tileSize.X, tileSize.Y));

        for (int x = centerCell.X - cellRadius; x <= centerCell.X + cellRadius; x++)
        {
            for (int y = centerCell.Y - cellRadius; y <= centerCell.Y + cellRadius; y++)
            {
                var cell = new Vector2I(x, y);

                // Skip if no hazard tile here
                if (HazardLayer.GetCellSourceId(cell) == -1)
                    continue;

                // Skip if outside radius
                if (HazardLayer.MapToLocal(cell).DistanceTo(playerPos) > radius)
                    continue;

                var origin = SnapToCluster(cell);

                if (!clusters.ContainsKey(origin))
                    clusters[origin] = new List<Vector2I>();

                clusters[origin].Add(cell);
            }
        }

        return clusters;
    }

    private Vector2I SnapToCluster(Vector2I cell)
    {
        return new Vector2I(
            Mathf.FloorToInt((float)cell.X / ClusterSize) * ClusterSize,
            Mathf.FloorToInt((float)cell.Y / ClusterSize) * ClusterSize
        );
    }
}
