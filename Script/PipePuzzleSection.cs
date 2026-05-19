using Godot;
using System.Collections.Generic;

public partial class PipePuzzleSection : Control
{
    // -------------------------------------------------------------------------
    // Exports — wire in editor
    // -------------------------------------------------------------------------
    [Export] private Label _feedbackLabel;
    [Export] private Button _closeButton;
    [Export] private GridContainer _grid;
    [Export] private Label _hintLabel;
    [Export] private int _tileSize = 80;

    // -------------------------------------------------------------------------
    // Asset paths — one PNG per pipe type, rotated in code
    // -------------------------------------------------------------------------
    private static readonly string[] PipeTextures = new string[]
    {
        "res://Asset/Pipe/pipe_cap.png",       // 0 = Cap
        "res://Asset/Pipe/pipe_straight.png",  // 1 = Straight
        "res://Asset/Pipe/pipe_elbow.png",     // 2 = Elbow
        "res://Asset/Pipe/pipe_t.png",         // 3 = T
        "res://Asset/Pipe/pipe_cross.png",     // 4 = Cross
    };

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------
    private int _cols;
    private int _rows;
    private int[] _types;
    private int[] _currentRot;
    private int[] _correctRot;
    private bool[] _isEmpty;      // true = blank tile, no pipe, not clickable
    private int _startCell;
    private int _endCell;

    private readonly List<TextureRect> _tiles = new();

    // -------------------------------------------------------------------------
    // Godot lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;
    }

    // -------------------------------------------------------------------------
    // Public API called by PuzzleUI
    // -------------------------------------------------------------------------
    public void Populate(Godot.Collections.Dictionary data)
    {
        _cols = data["cols"].AsInt32();
        _rows = data["rows"].AsInt32();
        _startCell = data["start"].AsInt32();
        _endCell = data["end"].AsInt32();

        var typesArr = data["types"].As<Godot.Collections.Array>();
        var correctArr = data["correct"].As<Godot.Collections.Array>();
        var emptyArr = data["empty"].As<Godot.Collections.Array>();

        int total = _cols * _rows;
        _types = new int[total];
        _correctRot = new int[total];
        _currentRot = new int[total];
        _isEmpty = new bool[total];

        for (int i = 0; i < total; i++)
        {
            _types[i] = typesArr[i].AsInt32();
            _correctRot[i] = correctArr[i].AsInt32();
            _isEmpty[i] = emptyArr[i].AsBool();
        }

        GD.Print("--- PIPE PUZZLE SOLUTION ---");
        for (int r = 0; r < _rows; r++)
        {
            string rowString = "";
            for (int c = 0; c < _cols; c++)
            {
                int idx = r * _cols + c;
                rowString += _correctRot[idx] + " ";
            }
            GD.Print($"Row {r}: {rowString}");
        }
        GD.Print("---------------------------");

        if (_hintLabel != null)
            _hintLabel.Text = data.ContainsKey("hint") ? data["hint"].AsString() : "";

        if (_feedbackLabel != null)
            _feedbackLabel.Text = "";

        BuildGrid();
        RandomiseRotations();
        ApplyAllRotations();
    }

    // -------------------------------------------------------------------------
    // Grid construction
    // -------------------------------------------------------------------------
    private void BuildGrid()
    {
        foreach (var t in _tiles) t.QueueFree();
        // Also clear all grid children (containers)
        foreach (Node child in _grid.GetChildren())
            child.QueueFree();
        _tiles.Clear();

        _grid.Columns = _cols;

        int total = _cols * _rows;
        for (int i = 0; i < total; i++)
        {
            int idx = i;

            // Outer container — always same size so grid stays uniform
            var container = new Control
            {
                CustomMinimumSize = new Vector2(_tileSize, _tileSize),
            };

            // Background panel — visible for all tiles
            var bg = new ColorRect
            {
                Size = new Vector2(_tileSize, _tileSize),
                Color = new Color(0.15f, 0.15f, 0.15f),
            };
            container.AddChild(bg);

            // Pipe texture — only for non-empty cells
            var rect = new TextureRect
            {
                Size = new Vector2(_tileSize, _tileSize),
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                PivotOffset = new Vector2(_tileSize / 2f, _tileSize / 2f),
                Position = Vector2.Zero,
            };

            if (!_isEmpty[i])
            {
                string path = PipeTextures[_types[i]];
                if (ResourceLoader.Exists(path))
                    rect.Texture = GD.Load<Texture2D>(path);

                // Tint start and end cells
                if (i == _startCell)
                    rect.Modulate = new Color(0.2f, 1f, 0.2f);   // green
                else if (i == _endCell)
                    rect.Modulate = new Color(1f, 0.4f, 0.1f);   // orange
            }

            container.AddChild(rect);

            // Clickable button — only wired for pipe cells
            if (!_isEmpty[i])
            {
                var btn = new Button
                {
                    Size = new Vector2(_tileSize, _tileSize),
                    Flat = true,
                };
                btn.Pressed += () => OnTileClicked(idx);
                container.AddChild(btn);
            }

            _grid.AddChild(container);
            _tiles.Add(rect);
        }
    }


    // -------------------------------------------------------------------------
    // Rotation logic
    // -------------------------------------------------------------------------
    private void RandomiseRotations()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < _currentRot.Length; i++)
        {
            // Empty or cross tiles — no rotation needed
            if (_isEmpty[i] || _types[i] == (int)PipeType.Cross)
            {
                _currentRot[i] = 0;
                continue;
            }

            // Randomise but guarantee start tile begins wrong
            int r;
            do { r = rng.RandiRange(0, 3); }
            while (i == _startCell && r == _correctRot[i]);
            _currentRot[i] = r;
        }
    }

    private void ApplyAllRotations()
    {
        for (int i = 0; i < _tiles.Count; i++)
            ApplyRotation(i);
    }

    private void ApplyRotation(int idx)
    {
        if (_isEmpty[idx]) return;
        _tiles[idx].RotationDegrees = _currentRot[idx] * 90f;
    }

    private void OnTileClicked(int idx)
    {
        if (_isEmpty[idx]) return;
        if (_types[idx] == (int)PipeType.Cross) return;

        _currentRot[idx] = (_currentRot[idx] + 1) % 4;
        ApplyRotation(idx);
        // 2. Debug the comparison
        int target = _correctRot[idx];
        int current = _currentRot[idx];

        // Calculate row/col for better log readability
        int r = idx / _cols;
        int c = idx % _cols;

        GD.Print($"--- Tile Clicked at ({r}, {c}) ---");
        GD.Print($"Current Rotation: {current} | Target Rotation: {target}");

        if (current == target)
        {
            GD.Print("Status: MATCHED (Logically Correct)");
        }
        else
        {
            GD.Print("Status: MISMATCHED");
        }

        // This is the trigger! It runs every time a user interacts.
        CheckSolved();
    }

    // -------------------------------------------------------------------------
    // Flood-fill connectivity check
    // -------------------------------------------------------------------------

    // Base openings at rot=0 per pipe type
    // Directions: 0=Up 1=Right 2=Down 3=Left
    private static readonly int[][] BaseOpenings = new int[][]
    {
        new[] { 2 },          // Cap:      Down
        new[] { 0, 2 },       // Straight: Up+Down
        new[] { 1, 2 },       // Elbow:    Right+Down
        new[] { 1, 2, 3 },    // T:        Right+Down+Left
        new[] { 0, 1, 2, 3 }, // Cross:    all
    };

    private HashSet<int> GetOpenings(int idx)
    {
        int type = _types[idx];
        int rot = _currentRot[idx];
        var result = new HashSet<int>();
        foreach (int dir in BaseOpenings[type])
            result.Add((dir + rot) % 4);
        return result;
    }

    private bool IsPathConnected()
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(_startCell);
        visited.Add(_startCell);

        int[] dr = { -1, 0, 1, 0 };
        int[] dc = { 0, 1, 0, -1 };
        int[] opp = { 2, 3, 0, 1 };

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (cur == _endCell) return true;

            int row = cur / _cols;
            int col = cur % _cols;

            var myOpenings = GetOpenings(cur);
            // GD.Print($"Cell {cur} at ({row},{col}) has openings: {string.Join(",", myOpenings)}");

            for (int dir = 0; dir < 4; dir++)
            {
                if (!myOpenings.Contains(dir)) continue;

                int nr = row + dr[dir];
                int nc = col + dc[dir];
                if (nr < 0 || nr >= _rows || nc < 0 || nc >= _cols) continue;

                int neighbourIdx = nr * _cols + nc;
                if (visited.Contains(neighbourIdx)) continue;
                if (_isEmpty[neighbourIdx]) continue;   // empty cells block flow
                if (!GetOpenings(neighbourIdx).Contains(opp[dir])) continue;

                visited.Add(neighbourIdx);
                queue.Enqueue(neighbourIdx);
            }
        }
        // GD.Print("are you checking?");

        return false;
    }

    private void CheckSolved()
    {
        // 1. Run the flood-fill check
        bool isPathConnected = IsPathConnected();
        // GD.Print($"Is path connected : {isPathConnected}");
        if (!isPathConnected) return;

        // 2. Visual feedback
        if (_feedbackLabel != null)
            _feedbackLabel.Text = "Pipes connected!";

        foreach (var tile in _tiles)
            tile.Modulate = new Color(0.3f, 1f, 0.5f);

        // 3. This calls TrySubmitAnswer, which now triggers ValidatePipe
        PuzzleManager.Instance.TrySubmitAnswer("solved");
    }

    // -------------------------------------------------------------------------
    // Close
    // -------------------------------------------------------------------------
    private void OnClosePressed()
    {
        PuzzleManager.Instance.CancelPuzzle();
        GetOwner<PuzzleUI>()?.HideAll();
    }
}

/// <summary>Pipe shape types — index must match PipeTextures array above.</summary>
public enum PipeType
{
    Cap = 0,
    Straight = 1,
    Elbow = 2,
    T = 3,
    Cross = 4,
}