using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Autoload: tracks divine trials, puzzle flow, and escape pod unlock (3 shrines).
/// </summary>
public partial class PuzzleManager : Node
{
	public static PuzzleManager Instance { get; private set; }

	public const int DefaultTrialsRequired = 3;

	[Export] public int TrialsRequired { get; set; } = DefaultTrialsRequired;

	public int TrialsSolved => _completedShrines.Count;

	public bool IsEscapePodUnlocked => TrialsSolved >= TrialsRequired;

	public PuzzleType? ActivePuzzleType => _activeType;

	[Signal]
	public delegate void TrialCompletedEventHandler(string shrineId);

	[Signal]
	public delegate void TrialFailedEventHandler(string shrineId);

	[Signal]
	public delegate void EscapePodUnlockedEventHandler();

	[Signal]
	public delegate void PuzzleStartedEventHandler(int puzzleType);

	private readonly HashSet<string> _completedShrines = new();

	private PuzzleType? _activeType;
	private Godot.Collections.Dictionary _activePayload;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	/// <summary>Mark a shrine trial done without a full puzzle flow (e.g. scripted completion).</summary>
	public void RegisterTrialSolved(string shrineId)
	{
		if (string.IsNullOrEmpty(shrineId))
			return;

		int beforeCount = TrialsSolved;
		if (!_completedShrines.Add(shrineId))
			return;

		EmitSignal(SignalName.TrialCompleted, shrineId);

		if (beforeCount < TrialsRequired && TrialsSolved >= TrialsRequired)
			EmitSignal(SignalName.EscapePodUnlocked);
	}

	/// <summary>Begin a puzzle session; payload should include "shrine_id" for trial registration on success.</summary>
	public void StartPuzzle(PuzzleType type, Godot.Collections.Dictionary payload)
	{
		_activeType = type;
		_activePayload = payload ?? new Godot.Collections.Dictionary();
		EmitSignal(SignalName.PuzzleStarted, (int)type);
	}

	public void CancelPuzzle()
	{
		_activeType = null;
		_activePayload = null;
	}

	/// <summary>Submit an answer for the active puzzle; on success registers shrine from payload "shrine_id".</summary>
	public bool TrySubmitAnswer(Variant answer)
	{
		if (_activeType is not { } type)
			return false;

		bool ok = type switch
		{
			PuzzleType.Math => ValidateMath(answer),
			PuzzleType.NumberSequence => ValidateSequence(answer),
			PuzzleType.ZeusRiddle => ValidateRiddle(answer),
			_ => false
		};

		var shrineId = _activePayload?.ContainsKey("shrine_id") == true
			? _activePayload["shrine_id"].AsString()
			: string.Empty;

		if (ok)
		{
			if (!string.IsNullOrEmpty(shrineId))
				RegisterTrialSolved(shrineId);
			_activeType = null;
			_activePayload = null;
		}
		else if (!string.IsNullOrEmpty(shrineId))
		{
			EmitSignal(SignalName.TrialFailed, shrineId);
		}

		return ok;
	}

	private bool ValidateMath(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("answer"))
			return false;

		double expected = ToDouble(_activePayload["answer"]);
		double got = ToDouble(answer);
		return Math.Abs(expected - got) < 1e-5;
	}

	private bool ValidateSequence(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("next"))
			return false;

		double expected = ToDouble(_activePayload["next"]);
		double got = ToDouble(answer);
		return Math.Abs(expected - got) < 1e-5;
	}

	private bool ValidateRiddle(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("answers"))
			return false;

		string input = answer.AsString().Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(input))
			return false;

		var arr = _activePayload["answers"].AsGodotArray();
		foreach (var item in arr)
		{
			string acceptable = item.AsString().Trim().ToLowerInvariant();
			if (input == acceptable)
				return true;
		}

		return false;
	}

	private static double ToDouble(Variant v)
	{
		if (v.VariantType == Variant.Type.Int)
			return v.AsInt32();
		if (v.VariantType == Variant.Type.Float)
			return v.AsDouble();
		if (v.VariantType == Variant.Type.String && double.TryParse(v.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
			return d;
		return double.NaN;
	}
}

public enum PuzzleType
{
	Math = 0,
	NumberSequence = 1,
	ZeusRiddle = 2
}
