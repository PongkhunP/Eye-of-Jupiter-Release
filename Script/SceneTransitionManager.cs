using Godot;
using System;

public partial class SceneTransitionManager : Node
{
	public static SceneTransitionManager Instance { get; private set; }

	private ColorRect _overlay;
	private Tween _tween;

	public override void _EnterTree() => Instance = this;
	public override void _ExitTree() { if (Instance == this) Instance = null; }

	public override void _Ready()
	{
		// Create full-screen black overlay on top of everything
		_overlay = new ColorRect();
		_overlay.Color = new Color(0, 0, 0, 0); // start transparent
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;

		var canvas = new CanvasLayer();
		canvas.Layer = 100; // always on top
		canvas.AddChild(_overlay);
		AddChild(canvas);
	}

	public async void TransitionToScene(string scenePath, float fadeDuration = 0.5f)
	{
		// Fade to black
		_tween = CreateTween();
		_tween.TweenProperty(_overlay, "color:a", 1f, fadeDuration);
		await ToSignal(_tween, Tween.SignalName.Finished);

		// Change scene
		GetTree().ChangeSceneToFile(scenePath);

		// Wait using a timer on THIS node (Autoload, survives scene change)
		var timer = GetTree().CreateTimer(0.1f);
		await ToSignal(timer, Timer.SignalName.Timeout);

		// Fade back in
		_tween = CreateTween();
		_tween.TweenProperty(_overlay, "color:a", 0f, fadeDuration);
	}
}
