using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Autoload: tracks divine trials, puzzle flow, and escape pod unlock (3 shrines).
/// </summary>
/// 
public partial class PuzzleManager : Node
{
	public static PuzzleManager Instance { get; private set; }

	public const int DefaultTrialsRequired = 3;
	public const string RiddleDataPath = "res://Data/riddles.json";
	public const string PuzzleUiScenePath = "res://UI/PuzzleUI.tscn";

	private PuzzleUI _ui;

	// How long (seconds) the player has per puzzle. Override per shrine if needed.
	public const float DefaultPuzzleTime = 30f;
	public const int MemoryTotalRounds = 5;

	// -------------------------------------------------------------------------
	// Exports
	// -------------------------------------------------------------------------
	[Export] public int TrialsRequired { get; set; } = DefaultTrialsRequired;
	[Export] public float PuzzleTimeLimit { get; set; } = DefaultPuzzleTime;

	// -------------------------------------------------------------------------
	// Public read-only state
	// -------------------------------------------------------------------------
	public int TrialsSolved => _completedShrines.Count;
	public bool IsEscapePodUnlocked => TrialsSolved >= TrialsRequired;
	public PuzzleType? ActivePuzzleType => _activeType;
	public float TimeRemaining => _timeRemaining;
	public bool IsPuzzleActive => _activeType.HasValue;

	// -------------------------------------------------------------------------
	// Signals
	// -------------------------------------------------------------------------

	/// <summary>Fired when a puzzle is ready to display. UI listens to this to populate itself.</summary>
	[Signal] public delegate void PuzzleStartedEventHandler(int puzzleType, Godot.Collections.Dictionary data);

	/// <summary>Fired every frame while a puzzle is active. UI uses this to update a countdown bar.</summary>
	[Signal] public delegate void PuzzleTimerTickedEventHandler(float secondsRemaining, float totalSeconds);

	/// <summary>Fired when the timer hits zero.</summary>
	[Signal] public delegate void PuzzleTimerExpiredEventHandler(string shrineId);

	[Signal] public delegate void TrialCompletedEventHandler(string shrineId);
	[Signal] public delegate void TrialFailedEventHandler(string shrineId);
	[Signal] public delegate void EscapePodUnlockedEventHandler();
	/// <summary>Fired at the start of each round. UI plays back the sequence then waits for input.</summary>
	[Signal] public delegate void MemorySequenceStartedEventHandler(int[] sequence, int round, int totalRounds);

	/// <summary>Fired when the player nails a round. UI shows next round's sequence.</summary>
	[Signal] public delegate void MemoryRoundPassedEventHandler(int[] sequence, int nextRound, int totalRounds);

	/// <summary>Fired when the player submits a wrong sequence.</summary>
	[Signal] public delegate void MemoryRoundFailedEventHandler();

	/// <summary>Fired when all 5 rounds are cleared.</summary>
	[Signal] public delegate void MemoryPuzzleCompletedEventHandler(string shrineId);

	// -------------------------------------------------------------------------
	// Private state
	// -------------------------------------------------------------------------
	private readonly HashSet<string> _completedShrines = new();
	private readonly List<RiddleEntry> _riddles = new();
	private readonly Random _rng = new();

	private PuzzleType? _activeType;
	private Godot.Collections.Dictionary _activePayload;   // answer data for validation
	private Godot.Collections.Dictionary _activePuzzleData; // display data sent to UI
	private string _activeShrineId;

	private float _timeRemaining;
	private float _totalTime;
	private bool _timerRunning;

	private int[] _memorySequence;
	private int _memoryRound;
	private string _memoryShrineId;


	// --------------------------------------------------- ----------------------
	// Godot lifecycle
	// -------------------------------------------------------------------------
	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Ready()
	{
		LoadRiddleData();
		CallDeferred(nameof(EnsureUi));
	}

	public override void _Process(double delta)
	{
		if (!_timerRunning) return;

		_timeRemaining -= (float)delta;

		EmitSignal(SignalName.PuzzleTimerTicked, _timeRemaining, _totalTime);

		if (_timeRemaining <= 0f)
			OnTimerExpired();
	}

	private void EnsureUi()
	{
		if (_ui != null)
			return;

		if (!ResourceLoader.Exists(PuzzleUiScenePath))
		{
			GD.PushWarning($"{nameof(PuzzleManager)}: missing {PuzzleUiScenePath}");
			return;
		}

		var inGameUI = GetTree().GetFirstNodeInGroup("ingame_ui") as CanvasLayer;
		if (inGameUI == null)
		{
			GD.PushWarning($"{nameof(PuzzleManager)}: InGameUI CanvasLayer not found");
			return;
		}

		var scene = GD.Load<PackedScene>(PuzzleUiScenePath);
		_ui = scene.Instantiate<PuzzleUI>();
		inGameUI.AddChild(_ui);
	}

	// -------------------------------------------------------------------------
	// JSON loading
	// -------------------------------------------------------------------------

	/// <summary>
	/// Expected riddles.json format:
	/// {
	///   "riddles": [
	///     { "id": "r01", "question": "What has roots as nobody sees?", "answers": ["mountain"] }
	///   ]
	/// }
	/// </summary>
	private void LoadRiddleData()
	{
		_riddles.Clear();

		if (!FileAccess.FileExists(RiddleDataPath))
		{
			GD.PushWarning($"{nameof(PuzzleManager)}: {RiddleDataPath} not found — using fallback riddles.");
			SeedFallbackRiddles();
			return;
		}

		using var file = FileAccess.Open(RiddleDataPath, FileAccess.ModeFlags.Read);
		if (file == null) { SeedFallbackRiddles(); return; }

		var variant = Json.ParseString(file.GetAsText());
		if (variant.VariantType != Variant.Type.Dictionary) { SeedFallbackRiddles(); return; }

		var root = variant.AsGodotDictionary();
		if (!root.ContainsKey("riddles")) { SeedFallbackRiddles(); return; }

		foreach (var item in root["riddles"].AsGodotArray())
		{
			var d = item.AsGodotDictionary();
			var entry = new RiddleEntry
			{
				Id = d.ContainsKey("id") ? d["id"].AsString() : Guid.NewGuid().ToString(),
				Question = d.ContainsKey("question") ? d["question"].AsString() : "",
				Answers = new List<string>()
			};

			if (d.ContainsKey("answers"))
				foreach (var a in d["answers"].AsGodotArray())
					entry.Answers.Add(a.AsString().Trim().ToLowerInvariant());

			if (entry.Answers.Count > 0)
				_riddles.Add(entry);
		}

		if (_riddles.Count == 0)
			SeedFallbackRiddles();
	}

	private void SeedFallbackRiddles()
	{
		_riddles.Add(new RiddleEntry
		{
			Id = "r_fallback_01",
			Question = "I have no legs yet I travel far, no mouth yet rivers fear my name. What am I?",
			Answers = new List<string> { "wind", "the wind" }
		});
		_riddles.Add(new RiddleEntry
		{
			Id = "r_fallback_02",
			Question = "The more you take, the more you leave behind. What am I?",
			Answers = new List<string> { "footsteps", "steps" }
		});
	}

	// -------------------------------------------------------------------------
	// Public API — start puzzles
	// -------------------------------------------------------------------------

	/// <summary>
	/// Start a Math puzzle. Operations: + - * / sqrt
	/// UI will receive PuzzleStarted signal with data:
	/// { "question": "12 + 7 = ?", "hint": "arithmetic" }
	/// </summary>
	public void StartMathPuzzle(string shrineId, float? timeOverride = null)
	{
		var (question, answer) = GenerateMathQuestion();

		_activePayload = new Godot.Collections.Dictionary
		{
			{ "shrine_id", shrineId },
			{ "answer",    answer   }
		};

		_activePuzzleData = new Godot.Collections.Dictionary
		{
			{ "question", question },
			{ "hint",     "Solve the equation" }
		};

		BeginPuzzle(PuzzleType.Math, shrineId, timeOverride);
	}

	/// <summary>
	/// Start a Number Sequence puzzle.
	/// UI data: { "question": "3, 5, 7, ?", "hint": "Find the next number" }
	/// </summary>
	public void StartSequencePuzzle(string shrineId, float? timeOverride = null)
	{
		var (question, next) = GenerateSequenceQuestion();

		_activePayload = new Godot.Collections.Dictionary
		{
			{ "shrine_id", shrineId },
			{ "next",      next     }
		};

		_activePuzzleData = new Godot.Collections.Dictionary
		{
			{ "question", question },
			{ "hint",     "What comes next?" }
		};

		BeginPuzzle(PuzzleType.NumberSequence, shrineId, timeOverride);
	}

	/// <summary>
	/// Start a Riddle puzzle picked randomly from loaded JSON.
	/// UI data: { "question": "...", "hint": "Think carefully" }
	/// </summary>
	public void StartRiddlePuzzle(string shrineId, float? timeOverride = null)
	{
		if (_riddles.Count == 0)
		{
			GD.PushWarning($"{nameof(PuzzleManager)}: no riddles loaded.");
			return;
		}

		var riddle = _riddles[_rng.Next(_riddles.Count)];

		var acceptedArr = new Godot.Collections.Array();
		foreach (var a in riddle.Answers) acceptedArr.Add(a);

		_activePayload = new Godot.Collections.Dictionary
		{
			{ "shrine_id", shrineId    },
			{ "answers",   acceptedArr }
		};

		_activePuzzleData = new Godot.Collections.Dictionary
		{
			{ "question", riddle.Question   },
			{ "hint",     "Answer in words" }
		};

		BeginPuzzle(PuzzleType.ZeusRiddle, shrineId, timeOverride);
	}
	/// <summary>
	/// Generates a full 5-step sequence and fires MemorySequenceStarted for round 1.
	/// Button indices: 0=TopLeft  1=TopRight  2=BottomLeft  3=BottomRight
	/// </summary>
	public void StartMemoryPuzzle(string shrineId, float? timeOverride = null)
	{
		_memoryShrineId = shrineId;
		_memoryRound = 1;
		_memorySequence = GenerateMemorySequence(MemoryTotalRounds);

		_activeType = PuzzleType.MemoryPuzzle;
		_activeShrineId = shrineId;

		StartTimer(timeOverride ?? PuzzleTimeLimit);

		EmitSignal(SignalName.MemorySequenceStarted,
			_memorySequence.Take(_memoryRound).ToArray(),
			_memoryRound,
			MemoryTotalRounds);
	}


	/// <summary>
	/// Start a digital lock puzzle.
	/// UI data: { "question": "Enter the 4-digit code", "hint": "...", "digits": 4 }
	/// Payload: { "lock_code": "1234" }
	/// </summary>
	public void StartOpenLockPuzzle(string shrineId, float? timeOverride = null)
	{
		int digits = 4;
		string code = GenerateLockCode(digits);
		GD.Print(code);

		_activePayload = new Godot.Collections.Dictionary
		{
			{ "shrine_id", shrineId },
			{ "lock_code", code     }
		};

		_activePuzzleData = new Godot.Collections.Dictionary
		{
			{ "question", $"Enter the {digits}-digit lock code" },
			{ "hint",     "Only digits are accepted" },
			{ "digits",   digits }
		};

		BeginPuzzle(PuzzleType.OpenTheLock, shrineId, float.PositiveInfinity);
	}

	/// <summary>Legacy entry point — kept for backward compatibility.</summary>
	public void StartPuzzle(PuzzleType type, Godot.Collections.Dictionary payload)
	{
		string shrineId = payload.ContainsKey("shrine_id") ? payload["shrine_id"].AsString() : "";

		switch (type)
		{
			case PuzzleType.Math: StartMathPuzzle(shrineId); break;
			case PuzzleType.NumberSequence: StartSequencePuzzle(shrineId); break;
			case PuzzleType.ZeusRiddle: StartRiddlePuzzle(shrineId); break;
			case PuzzleType.MemoryPuzzle: StartMemoryPuzzle(shrineId); break;
			case PuzzleType.OpenTheLock: StartOpenLockPuzzle(shrineId); break;
			case PuzzleType.PipePuzzle:
			case PuzzleType.PlanRoute:
				GD.PushWarning($"{nameof(PuzzleManager)}: {type} not implemented yet.");
				break;
		}
	}

	public void CancelPuzzle()
	{
		StopTimer();
		_activeType = null;
		_activePayload = null;
		_activePuzzleData = null;
		_activeShrineId = null;
		CancelMemoryState();
	}

	// -------------------------------------------------------------------------
	// Answer submission
	// -------------------------------------------------------------------------
	public bool TrySubmitAnswer(Variant answer)
	{
		if (_activeType is not { } type) return false;

		bool ok = type switch
		{
			PuzzleType.Math => ValidateMath(answer),
			PuzzleType.NumberSequence => ValidateSequence(answer),
			PuzzleType.ZeusRiddle => ValidateRiddle(answer),
			PuzzleType.OpenTheLock => ValidateOpenLock(answer),
			_ => false
		};

		string shrineId = _activeShrineId ?? string.Empty;

		if (ok)
		{
			StopTimer();
			if (!string.IsNullOrEmpty(shrineId))
				RegisterTrialSolved(shrineId);
			_activeType = null;
			_activePayload = null;
			_activePuzzleData = null;
			_activeShrineId = null;
		}
		else if (!string.IsNullOrEmpty(shrineId))
		{
			EmitSignal(SignalName.TrialFailed, shrineId);
		}

		return ok;
	}

	// -------------------------------------------------------------------------
	// Trial registration
	// -------------------------------------------------------------------------
	public void RegisterTrialSolved(string shrineId)
	{
		if (string.IsNullOrEmpty(shrineId)) return;

		int before = TrialsSolved;
		if (!_completedShrines.Add(shrineId)) return;

		EmitSignal(SignalName.TrialCompleted, shrineId);

		if (before < TrialsRequired && TrialsSolved >= TrialsRequired)
			EmitSignal(SignalName.EscapePodUnlocked);
	}

	// -------------------------------------------------------------------------
	// Internal helpers
	// -------------------------------------------------------------------------
	private void BeginPuzzle(PuzzleType type, string shrineId, float? timeOverride)
	{
		_activeType = type;
		_activeShrineId = shrineId;
		StartTimer(timeOverride ?? PuzzleTimeLimit);
		EmitSignal(SignalName.PuzzleStarted, (int)type, _activePuzzleData);
	}

	private void StartTimer(float seconds)
	{
		_totalTime = seconds;
		_timeRemaining = seconds;
		_timerRunning = true;
	}

	private void StopTimer() => _timerRunning = false;

	private void OnTimerExpired()
	{
		StopTimer();
		string shrineId = _activeShrineId ?? _memoryShrineId ?? string.Empty;
		CancelPuzzle();
		EmitSignal(SignalName.PuzzleTimerExpired, shrineId);
		if (!string.IsNullOrEmpty(shrineId))
			EmitSignal(SignalName.TrialFailed, shrineId);
	}

	// -------------------------------------------------------------------------
	// Math generator
	// -------------------------------------------------------------------------

	/// <summary>
	/// Generates a math question using +, -, *, /, sqrt.
	/// Returns (question string, correct answer as double).
	/// </summary>
	private (string question, double answer) GenerateMathQuestion()
	{
		// Pick operation: 0=add, 1=sub, 2=mul, 3=div, 4=sqrt
		int op = _rng.Next(5);

		switch (op)
		{
			case 0: // Addition
				{
					int a = _rng.Next(1, 50), b = _rng.Next(1, 50);
					return ($"{a} + {b} = ?", a + b);
				}
			case 1: // Subtraction (ensure non-negative result)
				{
					int a = _rng.Next(10, 100), b = _rng.Next(1, a);
					return ($"{a} - {b} = ?", a - b);
				}
			case 2: // Multiplication
				{
					int a = _rng.Next(2, 12), b = _rng.Next(2, 12);
					return ($"{a} × {b} = ?", a * b);
				}
			case 3: // Division (whole number result only)
				{
					int b = _rng.Next(2, 10);
					int result = _rng.Next(2, 12);
					int a = b * result;
					return ($"{a} ÷ {b} = ?", result);
				}
			default: // Square root (perfect squares 1–144)
				{
					int[] perfectSquares = { 1, 4, 9, 16, 25, 36, 49, 64, 81, 100, 121, 144 };
					int sq = perfectSquares[_rng.Next(perfectSquares.Length)];
					int root = (int)Math.Sqrt(sq);
					return ($"√{sq} = ?", root);
				}
		}
	}

	// -------------------------------------------------------------------------
	// Sequence generator
	// -------------------------------------------------------------------------

	/// <summary>
	/// Generates a number sequence question.
	/// Patterns: arithmetic, geometric, Fibonacci-style, squares, alternating step.
	/// Returns (question string, correct next value).
	/// </summary>
	private (string question, double next) GenerateSequenceQuestion()
	{
		int pattern = _rng.Next(5);

		switch (pattern)
		{
			case 0: // Arithmetic  e.g. 3, 7, 11, 15, _
				{
					int start = _rng.Next(1, 20);
					int step = _rng.Next(2, 10);
					var seq = new int[4];
					for (int i = 0; i < 4; i++) seq[i] = start + step * i;
					double next = start + step * 4;
					return (FormatSequence(seq) + ", _", next);
				}
			case 1: // Geometric  e.g. 2, 6, 18, 54, _
				{
					int start = _rng.Next(1, 5);
					int ratio = _rng.Next(2, 4);
					var seq = new int[4];
					seq[0] = start;
					for (int i = 1; i < 4; i++) seq[i] = seq[i - 1] * ratio;
					double next = seq[3] * ratio;
					return (FormatSequence(seq) + ", _", next);
				}
			case 2: // Fibonacci-style  e.g. 1, 1, 2, 3, _
				{
					int a = _rng.Next(1, 5), b = _rng.Next(1, 5);
					int c = a + b, d = b + c;
					double next = c + d;
					return ($"{a}, {b}, {c}, {d}, _", next);
				}
			case 3: // Squares  e.g. 1, 4, 9, 16, _
				{
					int start = _rng.Next(1, 6);
					var seq = new int[4];
					for (int i = 0; i < 4; i++) seq[i] = (start + i) * (start + i);
					double next = (start + 4) * (start + 4);
					return (FormatSequence(seq) + ", _", next);
				}
			default: // Alternating step  e.g. 1, 3, 6, 8, 11, _
				{
					int start = _rng.Next(1, 10);
					int stepA = _rng.Next(2, 5);
					int stepB = _rng.Next(1, stepA);
					var seq = new int[5];
					seq[0] = start;
					for (int i = 1; i < 5; i++)
						seq[i] = seq[i - 1] + (i % 2 == 1 ? stepA : stepB);
					double next = seq[4] + stepA; // next step is stepA
					return (FormatSequence(seq) + ", _", next);
				}
		}
	}

	private void CancelMemoryState()
	{
		_memorySequence = null;
		_memoryShrineId = null;
		_memoryRound = 0;
	}

	// -------------------------------------------------------------------------
	// Generators
	// -------------------------------------------------------------------------
	private int[] GenerateMemorySequence(int length)
	{
		var seq = new int[length];
		for (int i = 0; i < length; i++)
			seq[i] = _rng.Next(4); // 0=TL 1=TR 2=BL 3=BR
		return seq;
	}

	private static string FormatSequence(int[] arr) => string.Join(", ", arr);

	// -------------------------------------------------------------------------
	// Validators
	// -------------------------------------------------------------------------
	private bool ValidateMath(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("answer")) return false;
		return Math.Abs(ToDouble(_activePayload["answer"]) - ToDouble(answer)) < 1e-5;
	}

	private bool ValidateSequence(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("next")) return false;
		return Math.Abs(ToDouble(_activePayload["next"]) - ToDouble(answer)) < 1e-5;
	}

	private bool ValidateRiddle(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("answers")) return false;

		string input = answer.AsString().Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(input)) return false;

		foreach (var item in _activePayload["answers"].AsGodotArray())
			if (input == item.AsString().Trim().ToLowerInvariant())
				return true;

		return false;
	}

	public void ValidateMemoryAnswer(int[] playerSequence)
	{
		if (_activeType != PuzzleType.MemoryPuzzle || _memorySequence == null)
			return;

		var expected = _memorySequence.Take(_memoryRound).ToArray();

		if (!expected.SequenceEqual(playerSequence))
		{
			// Wrong sequence — fail
			StopTimer();
			EmitSignal(SignalName.MemoryRoundFailed);
			EmitSignal(SignalName.TrialFailed, _memoryShrineId ?? "");
			_activeType = null;
			CancelMemoryState();
			return;
		}

		// Correct!
		if (_memoryRound >= MemoryTotalRounds)
		{
			// All rounds cleared — puzzle complete
			StopTimer();
			string shrineId = _memoryShrineId ?? "";
			_activeType = null;
			CancelMemoryState();
			RegisterTrialSolved(shrineId);
			EmitSignal(SignalName.MemoryPuzzleCompleted, shrineId);
			return;
		}

		// Advance to next round
		_memoryRound++;
		StopTimer();
		StartTimer(PuzzleTimeLimit);

		EmitSignal(SignalName.MemoryRoundPassed,
			_memorySequence.Take(_memoryRound).ToArray(),
			_memoryRound,
			MemoryTotalRounds);
	}
	private bool ValidateOpenLock(Variant answer)
	{
		if (_activePayload == null || !_activePayload.ContainsKey("lock_code"))
			return false;

		string expected = _activePayload["lock_code"].AsString().Trim();
		string input = answer.AsString().Trim();

		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expected))
			return false;

		foreach (char c in input)
			if (!char.IsDigit(c))
				return false;

		return input == expected;
	}

	private static double ToDouble(Variant v)
	{
		if (v.VariantType == Variant.Type.Int) return v.AsInt32();
		if (v.VariantType == Variant.Type.Float) return v.AsDouble();
		if (v.VariantType == Variant.Type.String &&
			double.TryParse(v.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
			return d;
		return double.NaN;
	}

	private string GenerateLockCode(int digits)
	{
		Span<char> buffer = stackalloc char[digits];
		for (int i = 0; i < digits; i++)
		{
			int d = _rng.Next(0, 10);
			buffer[i] = (char)('0' + d);
		}
		return new string(buffer);
	}

	public bool IsShrineSolved(string shrineId)
	{
		return _completedShrines.Contains(shrineId);
	}

	// -------------------------------------------------------------------------
	// Inner types
	// -------------------------------------------------------------------------
	private class RiddleEntry
	{
		public string Id;
		public string Question;
		public List<string> Answers;
	}
}

public enum PuzzleType
{
	Math = 0,
	NumberSequence = 1,
	ZeusRiddle = 2,
	PipePuzzle = 3,
	MemoryPuzzle = 4,
	OpenTheLock = 5,
	PlanRoute = 6
}
