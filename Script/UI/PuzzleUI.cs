using Godot;
using System;

public partial class PuzzleUI : Control
{
	// ---- sub-panel references (assign in editor via [Export]) ----------------
	[Export] private Control _mathSection;
	[Export] private Control _riddleSection;
	[Export] private Control _memorySection;

	// ---- timer bar (shared, lives on PuzzleUI root level) -------------------
	[Export] private ProgressBar _timerBar;   // or ProgressBar
	[Export] private Label _timerLabel;

	public override void _Ready()
	{
		// Connect to PuzzleManager signals
		PuzzleManager.Instance.PuzzleStarted += OnPuzzleStarted;
		PuzzleManager.Instance.PuzzleTimerTicked += OnTimerTicked;
		PuzzleManager.Instance.PuzzleTimerExpired += OnTimerExpired;
		PuzzleManager.Instance.TrialCompleted += OnTrialCompleted;
		PuzzleManager.Instance.TrialFailed += OnTrialFailed;

		PuzzleManager.Instance.MemorySequenceStarted += OnMemorySequenceStarted;
		PuzzleManager.Instance.MemoryRoundPassed += OnMemoryRoundPassed;
		PuzzleManager.Instance.MemoryRoundFailed += OnMemoryRoundFailed;
		PuzzleManager.Instance.MemoryPuzzleCompleted += OnMemoryPuzzleCompleted;

		HideAll();
	}

	public override void _ExitTree()
	{
		if (PuzzleManager.Instance == null) return;
		PuzzleManager.Instance.PuzzleStarted -= OnPuzzleStarted;
		PuzzleManager.Instance.PuzzleTimerTicked -= OnTimerTicked;
		PuzzleManager.Instance.PuzzleTimerExpired -= OnTimerExpired;
		PuzzleManager.Instance.TrialCompleted -= OnTrialCompleted;
		PuzzleManager.Instance.TrialFailed -= OnTrialFailed;

		PuzzleManager.Instance.MemorySequenceStarted -= OnMemorySequenceStarted;
		PuzzleManager.Instance.MemoryRoundPassed -= OnMemoryRoundPassed;
		PuzzleManager.Instance.MemoryRoundFailed -= OnMemoryRoundFailed;
		PuzzleManager.Instance.MemoryPuzzleCompleted -= OnMemoryPuzzleCompleted;
	}

	// -------------------------------------------------------------------------
	// Signal handlers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Called by PuzzleManager when a puzzle begins.
	/// data keys: "question" (string), "hint" (string)
	/// </summary>
	private void OnPuzzleStarted(int puzzleType, Godot.Collections.Dictionary data)
	{
		HideAll();

		string question = data.ContainsKey("question") ? data["question"].AsString() : "";
		string hint = data.ContainsKey("hint") ? data["hint"].AsString() : "";

		switch ((PuzzleType)puzzleType)
		{
			case PuzzleType.Math:
			case PuzzleType.NumberSequence:
				// Both use the math section (LineEdit + numeric answer)
				if (_mathSection is MathPuzzleSection math)
					math.Populate(question, hint);
				_mathSection.Visible = true;
				break;

			case PuzzleType.ZeusRiddle:
				if (_riddleSection is RiddlePuzzleSection riddle)
					riddle.Populate(question, hint);
				_riddleSection.Visible = true;
				break;
		}

		Visible = true;
	}

	private void OnTimerTicked(float remaining, float total)
	{
		if (_timerBar != null) _timerBar.Value = remaining / total * 100f;
		if (_timerLabel != null) _timerLabel.Text = $"{Mathf.CeilToInt(remaining)}s";
	}

	private void OnTimerExpired(string shrineId)
	{
		HideAll();
		// Optionally flash a "Time's up!" message before hiding
	}

	private void OnTrialCompleted(string shrineId) => HideAll();

	private void OnTrialFailed(string shrineId)
	{
		// Keep the panel open so the player can retry, OR hide — your choice.
		// For now just hide on fail too:
		// HideAll();
	}

	private void OnMemorySequenceStarted(int[] sequence, int round, int totalRounds)
	{
		HideAll();
		if (_memorySection is MemoryPuzzleSection memory)
			memory.OnSequenceStarted(sequence, round, totalRounds);
		_memorySection.Visible = true;
		Visible = true;
	}

	private void OnMemoryRoundPassed(int[] sequence, int nextRound, int totalRounds)
	{
		if (_memorySection is MemoryPuzzleSection memory)
			memory.OnRoundPassed(sequence, nextRound, totalRounds);
	}

	private void OnMemoryRoundFailed()
	{
		if (_memorySection is MemoryPuzzleSection memory)
			memory.OnRoundFailed();
	}

	private void OnMemoryPuzzleCompleted(string shrineId)
	{
		if (_memorySection is MemoryPuzzleSection memory)
			memory.OnPuzzleCompleted(shrineId);
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------
	private void HideAll()
	{
		Visible = false;
		if (_mathSection != null) _mathSection.Visible = false;
		if (_riddleSection != null) _riddleSection.Visible = false;
		if (_memorySection != null) _memorySection.Visible = false;
	}
}
