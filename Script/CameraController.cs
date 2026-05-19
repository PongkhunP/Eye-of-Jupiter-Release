using Godot;

public partial class CameraController : Camera2D
{
	public static CameraController Instance { get; private set; }

	// ── Shake ─────────────────────────────────────────────────────
	private float _shakeDuration = 0f;
	private float _shakeIntensity = 0f;
	private Vector2 _originalOffset;

	// ── Zeus Indicator ────────────────────────────────────────────
	[Export] public float EdgePadding { get; set; } = 40f;
	[Export] public float ArrowSize { get; set; } = 20f;
	[Export] public Color ArrowColor { get; set; } = new Color(1f, 0.85f, 0.1f, 0.9f);
	[Export] public bool ShowDistance { get; set; } = true;

	private ZeusBodyController _zeus;
	private Vector2 _screenSize;
	private ScreenIndicator _indicator;

	public override void _EnterTree() => Instance = this;
	public override void _ExitTree() { if (Instance == this) Instance = null; }

	// In CameraController._Ready() replace ScreenIndicator with CanvasLayer approach
	public override void _Ready()
	{
		_originalOffset = Offset;
		_zeus = GetTree().GetFirstNodeInGroup("zeus") as ZeusBodyController;

		// CanvasLayer ensures pure screen space drawing
		var canvas = new CanvasLayer();
		canvas.Layer = 10;
		AddChild(canvas);

		_indicator = new ScreenIndicator();
		_indicator.Camera = this;
		_indicator.GetZeus = () => _zeus;
		_indicator.EdgePadding = EdgePadding;
		_indicator.ArrowSize = ArrowSize;
		_indicator.ArrowColor = ArrowColor;
		_indicator.ShowDistance = ShowDistance;
		canvas.AddChild(_indicator); // ← add to CanvasLayer, not directly

		GD.Print($"Zeus found: {_zeus != null}");
	}

	public override void _Process(double delta)
	{
		// ── Shake ─────────────────────────────────────────────────
		if (_shakeDuration > 0f)
		{
			_shakeDuration -= (float)delta;
			Offset = new Vector2(
				(float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
				(float)GD.RandRange(-_shakeIntensity, _shakeIntensity)
			);
			_shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, (float)delta * 5f);
			if (_shakeDuration <= 0f)
				Offset = _originalOffset;
		}

		// ── Indicator redraw ──────────────────────────────────────
		if (_zeus != null && IsInstanceValid(_zeus))
		{
			_screenSize = GetViewportRect().Size;
			QueueRedraw();
		}
	}

	public void Shake(float duration, float intensity)
	{
		_shakeDuration = duration;
		_shakeIntensity = intensity;
	}
}