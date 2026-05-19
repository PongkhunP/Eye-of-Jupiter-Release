using Godot;
using System;
using System.Collections.Generic;

public partial class PlanRouteSection : Control
{
    // -------------------------------------------------------------------------
    // Exports
    // -------------------------------------------------------------------------
    [Export] private Texture2D _asteroidTexture;
    [Export] private Label _heartLabel;
    [Export] private Label _hintLabel;
    [Export] private Label _feedbackLabel;
    [Export] private Button _closeButton;
    [Export] private Button _resetButton;
    [Export] private Panel _planRoutePanel;
    [Export] private ProgressBar _fuelBar;    // wire in editor
    [Export] private Label _fuelLabel;  // wire in editor

    [Export] private float _snapRadius = 30f; // pixel distance to snap to End

    // -------------------------------------------------------------------------
    // Runtime state — positions stored in PIXEL space after Populate()
    // -------------------------------------------------------------------------
    private Vector2 _startPos;
    private Vector2 _endPos;
    private Rect2 _bounds;

    // Per-asteroid data
    private struct AsteroidData
    {
        public Vector2 Pos;
        public float Radius;    // collision + draw radius
        public float Rotation;  // degrees, for texture drawing
        public float Scale;     // draw scale
    }
    private readonly List<AsteroidData> _asteroids = new();
    private readonly List<Vector2> _drawnPath = new();

    private int _hearts = 3;
    private bool _puzzleDone = false;
    private float _fuel = 1f;   // 0..1, full = 1
    private float _maxFuelDist = 0f;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    public override void _Ready()
    {
        if (_closeButton != null) _closeButton.Pressed += OnClosePressed;
        if (_resetButton != null) _resetButton.Pressed += ResetPath;

        // Let clicks pass through the panel to this Control
        if (_planRoutePanel != null)
            _planRoutePanel.MouseFilter = MouseFilterEnum.Ignore;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------
    public void Populate(Godot.Collections.Dictionary data)
    {
        _puzzleDone = false;
        _drawnPath.Clear();
        _asteroids.Clear();

        // Resolve bounds — wait one frame if panel hasn't laid out yet
        _bounds = _planRoutePanel != null
            ? _planRoutePanel.GetRect()
            : new Rect2(Vector2.Zero, Size);

        // PCG gives normalised 0..1 positions — remap to pixel bounds
        _startPos = Remap(data["start_pos"].AsVector2());
        _endPos = Remap(data["end_pos"].AsVector2());

        // Build per-asteroid data with random size + rotation
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        foreach (var v in data["asteroids"].AsGodotArray())
        {
            // Random radius between 20 and 48 px
            float radius = rng.RandfRange(20f, 48f);
            float scale = radius / 32f; // normalise against base ~32px tex size
            float rot = rng.RandfRange(0f, 360f);

            _asteroids.Add(new AsteroidData
            {
                Pos = Remap(v.AsVector2()),
                Radius = radius,
                Scale = scale,
                Rotation = rot,
            });
        }

        _hearts = 3;
        _drawnPath.Add(_startPos);

        if (_hintLabel != null) _hintLabel.Text = data.ContainsKey("hint") ? data["hint"].AsString() : "";
        if (_feedbackLabel != null) _feedbackLabel.Text = "Click to draw your route to End.";

        _maxFuelDist = _startPos.DistanceTo(_endPos) * 1.5f;
        _fuel = 1f;
        UpdateFuelBar();

        UpdateHeartLabel();
        QueueRedraw();
    }

    // Normalised 0..1 → pixel position within bounds
    private Vector2 Remap(Vector2 norm)
        => _bounds.Position + norm * _bounds.Size;

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------
    public override void _GuiInput(InputEvent @event)
    {
        if (_puzzleDone) return;
        if (@event is not InputEventMouseButton mb) return;
        if (!mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        if (Size.X <= 0 || Size.Y <= 0) return;

        // Clamp click within panel bounds then use directly (pixel space)
        Vector2 click = mb.Position.Clamp(
            _bounds.Position,
            _bounds.Position + _bounds.Size);

        AddWaypoint(click);
    }

    // -------------------------------------------------------------------------
    // Waypoint logic
    // -------------------------------------------------------------------------
    private void AddWaypoint(Vector2 point)
    {
        Vector2 prev = _drawnPath[^1];

        bool hit = false;
        foreach (var ast in _asteroids)
        {
            if (SegmentIntersectsCircle(prev, point, ast.Pos, ast.Radius))
            {
                hit = true;
                break;
            }
        }

        // Always add point first so player sees the segment
        _drawnPath.Add(point);
        QueueRedraw();

        if (hit)
        {
            _hearts--;
            UpdateHeartLabel();

            if (_hearts <= 0)
            {
                _puzzleDone = true;
                if (_feedbackLabel != null) _feedbackLabel.Text = "Out of hearts! Puzzle failed.";
                GetTree().CreateTimer(1.5f).Timeout += OnFailTimeout;
            }
            else
            {
                if (_feedbackLabel != null)
                    _feedbackLabel.Text = $"Hit an asteroid! {_hearts} hearts left. Path reset!";
                ResetPath();
            }
            return;
        }

        if (_feedbackLabel != null) _feedbackLabel.Text = "Safe! Keep going.";

        // ---- Snap check — pure pixel distance now ----
        if (point.DistanceTo(_endPos) <= _snapRadius)
        {
            _drawnPath[^1] = _endPos;
            _puzzleDone = true;
            if (_feedbackLabel != null) _feedbackLabel.Text = "Route planned! Launching...";
            QueueRedraw();
            PuzzleManager.Instance.TrySubmitAnswer("solved");
        }

        float segmentCost = prev.DistanceTo(point) / _maxFuelDist;
        _fuel = Mathf.Max(0f, _fuel - segmentCost);
        UpdateFuelBar();

        if (_fuel <= 0f)
        {
            _drawnPath.Add(point);
            QueueRedraw();
            _puzzleDone = true;
            if (_feedbackLabel != null) _feedbackLabel.Text = "Out of fuel! Puzzle failed.";
            GetTree().CreateTimer(1.5f).Timeout += OnFailTimeout;
            return;
        }
    }

    // -------------------------------------------------------------------------
    // Reset
    // -------------------------------------------------------------------------
    private void ResetPath()
    {
        if (_puzzleDone) return;
        _drawnPath.Clear();
        _drawnPath.Add(_startPos);
        if (_feedbackLabel != null && _hearts > 0)
            _feedbackLabel.Text = $"Path reset. {_hearts} hearts left. Try again!";
        _fuel = 1f;
        UpdateFuelBar();
        QueueRedraw();
    }

    // -------------------------------------------------------------------------
    // Fail
    // -------------------------------------------------------------------------
    private void OnFailTimeout()
    {
        PuzzleManager.Instance.CancelPuzzle();
        GetOwner<PuzzleUI>()?.HideAll();
    }

    private void UpdateFuelBar()
    {
        if (_fuelBar != null) _fuelBar.Value = _fuel * 100f;  // ProgressBar expects 0..100
        if (_fuelLabel != null) _fuelLabel.Text = $"Fuel: {Mathf.RoundToInt(_fuel * 100f)}%";
    }

    // -------------------------------------------------------------------------
    // Drawing
    // -------------------------------------------------------------------------
    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;

        // Dark background clipped to panel bounds
        DrawRect(_bounds, new Color(0.05f, 0.05f, 0.10f));

        // Asteroids
        foreach (var ast in _asteroids)
        {
            if (_asteroidTexture != null)
            {
                Vector2 texSize = _asteroidTexture.GetSize();
                Vector2 drawSize = texSize * ast.Scale;
                var xform = Transform2D.Identity
                    .Translated(ast.Pos)
                    .Rotated(Mathf.DegToRad(ast.Rotation))
                    .Translated(-drawSize / 2f);

                DrawSetTransform(ast.Pos, Mathf.DegToRad(ast.Rotation), Vector2.One * ast.Scale);
                DrawTexture(_asteroidTexture, -texSize / 2f);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One); // reset transform
            }
            else
            {
                // Fallback grey circle
                DrawCircle(ast.Pos, ast.Radius, new Color(0.45f, 0.45f, 0.50f));
                DrawArc(ast.Pos, ast.Radius, 0, Mathf.Tau, 32, new Color(0.65f, 0.65f, 0.70f), 2f);
            }
        }

        // Drawn path
        for (int i = 0; i < _drawnPath.Count - 1; i++)
        {
            DrawLine(_drawnPath[i], _drawnPath[i + 1], new Color(0.3f, 0.85f, 1f), 3f);
            DrawCircle(_drawnPath[i + 1], 5f, new Color(0.3f, 0.85f, 1f));
        }

        // Start
        DrawCircle(_startPos, 14f, Colors.Green);
        DrawString(ThemeDB.FallbackFont, _startPos + new Vector2(16, 5),
            "Start", HorizontalAlignment.Left, -1, 14, Colors.White);

        // End — pulse ring to make it obvious
        DrawCircle(_endPos, 14f, Colors.Orange);
        DrawArc(_endPos, _snapRadius, 0, Mathf.Tau, 48,
            new Color(1f, 0.6f, 0.1f, 0.35f), 2f); // snap radius hint
        DrawString(ThemeDB.FallbackFont, _endPos + new Vector2(16, 5),
            "End", HorizontalAlignment.Left, -1, 14, Colors.White);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private void UpdateHeartLabel()
    {
        if (_heartLabel != null) _heartLabel.Text = $"Hearts: {_hearts}";
    }

    private static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, Vector2 center, float radius)
    {
        Vector2 ab = b - a;
        Vector2 ac = center - a;
        float t = Mathf.Clamp(ac.Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return center.DistanceTo(a + t * ab) < radius;
    }

    private void OnClosePressed()
    {
        PuzzleManager.Instance.CancelPuzzle();
        GetOwner<PuzzleUI>()?.HideAll();
    }
}
