using Godot;
using System;
using System.Collections.Generic;

public partial class MemoryPuzzleSection : Control
{
	// ---- exports ------------------------------------------------------------
	[Export] private Button _btnTopLeft;
	[Export] private Button _btnTopRight;
	[Export] private Button _btnBottomLeft;
	[Export] private Button _btnBottomRight;
	[Export] private Button _submitButton;
	[Export] private Label _roundLabel;
	[Export] private Label _statusLabel;

	// ---- colors -------------------------------------------------------------
	// Active (lit) colors per button index
	private static readonly Color[] ActiveColors = {
		new Color("00FF00"),  // 0 TL bright green
        new Color("FFD700"),  // 1 TR bright gold/yellow
        new Color("00BFFF"),  // 2 BL bright sky blue
        new Color("FF2020"),  // 3 BR bright red
    };

	// ---- Visible but clearly "off" dim colors --------------------------------
	private static readonly Color[] DimColors = {
		new Color("1A3D1A"),  // 0 TL dim green
        new Color("3D3000"),  // 1 TR dim yellow
        new Color("00264D"),  // 2 BL dim blue
        new Color("3D0000"),  // 3 BR dim red
    };

	private const float InitialDelay = 1.0f;  // delay before first flash starts
	private const float FlashOnTime = 0.6f;  // button stays lit
	private const float FlashOffTime = 0.3f;  // gap between buttons
	private const float RoundDelay = 1.2f;  // pause before next round plays

	// ---- runtime state ------------------------------------------------------
	private Button[] _buttons;
	private int[] _currentSequence;
	private List<int> _playerInput = new();
	private bool _isPlayingBack = false;
	private bool _isPlayersTurn = false;

	// Tween for playback sequencing
	private int _playbackIndex = 0;
	private float _playbackTimer = 0f;
	private bool _flashOn = false;

	// =========================================================================
	// Godot lifecycle
	// =========================================================================
	public override void _Ready()
	{
		_buttons = new[] { _btnTopLeft, _btnTopRight, _btnBottomLeft, _btnBottomRight };

		// Wire player button presses
		for (int i = 0; i < _buttons.Length; i++)
		{
			int captured = i; // capture for lambda
			_buttons[i].Pressed += () => OnPlayerPressedButton(captured);
		}

		_submitButton.Pressed += OnSubmitPressed;

		// Connect PuzzleManager signals
		PuzzleManager.Instance.MemorySequenceStarted += OnSequenceStarted;
		PuzzleManager.Instance.MemoryRoundPassed += OnRoundPassed;
		PuzzleManager.Instance.MemoryRoundFailed += OnRoundFailed;
		PuzzleManager.Instance.MemoryPuzzleCompleted += OnPuzzleCompleted;

		SetAllDim();
		SetInteractable(false);
		_submitButton.Visible = false;
	}

	public override void _ExitTree()
	{
		if (PuzzleManager.Instance == null) return;
		PuzzleManager.Instance.MemorySequenceStarted -= OnSequenceStarted;
		PuzzleManager.Instance.MemoryRoundPassed -= OnRoundPassed;
		PuzzleManager.Instance.MemoryRoundFailed -= OnRoundFailed;
		PuzzleManager.Instance.MemoryPuzzleCompleted -= OnPuzzleCompleted;
	}

	public override void _Process(double delta)
	{
		if (!_isPlayingBack) return;

		_playbackTimer -= (float)delta;
		if (_playbackTimer > 0f) return;

		if (_flashOn)
		{
			SetButtonDim(_currentSequence[_playbackIndex]);
			_flashOn = false;
			_playbackIndex++;

			if (_playbackIndex >= _currentSequence.Length)
			{
				// Playback done — player's turn
				_isPlayingBack = false;
				_isPlayersTurn = true;
				SetInteractable(true);
				_submitButton.Visible = true;
				SetStatus("Your turn! Repeat the sequence then press Submit.");
				return;
			}

			_playbackTimer = FlashOffTime;
		}
		else
		{
			SetButtonLit(_currentSequence[_playbackIndex]);
			_flashOn = true;
			_playbackTimer = FlashOnTime;
		}
	}

	// =========================================================================
	// Signal handlers
	// =========================================================================
	public void OnSequenceStarted(int[] sequence, int round, int totalRounds)
	{
		Visible = true;
		StartPlayback(sequence, round, totalRounds);
	}

	public void OnRoundPassed(int[] sequence, int nextRound, int totalRounds)
	{
		SetStatus($"✓ Correct! Round {nextRound} starting...");
        SetInteractable(false);
        _submitButton.Visible = false;

        // Delay before playing next round so player can breathe
        GetTree().CreateTimer(RoundDelay).Timeout += () =>
            StartPlayback(sequence, nextRound, totalRounds);
	}

	public void OnRoundFailed()
	{
		SetStatus("✗ Wrong sequence! Puzzle failed.");
        SetInteractable(false);
        _submitButton.Visible = false;
        SetAllDim();
        GetTree().CreateTimer(2.0f).Timeout += () => Visible = false;
	}

	public void OnPuzzleCompleted(string shrineId)
	{
		SetStatus("✓ All rounds cleared! Shrine unlocked.");
        SetInteractable(false);
        _submitButton.Visible = false;
        SetAllDim();
        GetTree().CreateTimer(2.0f).Timeout += () => Visible = false;
	}

	// =========================================================================
	// Player input
	// =========================================================================
	private void OnPlayerPressedButton(int index)
	{
		if (!_isPlayersTurn) return;

        _playerInput.Add(index);
        SetStatus($"Input: {_playerInput.Count} / {_currentSequence.Length}");

        // Flash the pressed button so player gets visual feedback
        SetButtonLit(index);
        GetTree().CreateTimer(0.2f).Timeout += () => SetButtonDim(index);
	}

	private void OnSubmitPressed()
	{
		if (!_isPlayersTurn) return;

		_isPlayersTurn = false;
		SetInteractable(false);
		_submitButton.Visible = false;

		PuzzleManager.Instance.ValidateMemoryAnswer(_playerInput.ToArray());
		_playerInput.Clear();
	}

	// =========================================================================
	// Playback helpers
	// =========================================================================
	private void StartPlayback(int[] sequence, int round, int totalRounds)
	{
		_currentSequence = sequence;
		_playerInput.Clear();
		_isPlayingBack = true;
		_isPlayersTurn = false;
		_playbackIndex = 0;
		_flashOn = false;
		_playbackTimer = InitialDelay; // small delay before first flash

		SetInteractable(false);
		_submitButton.Visible = false;
		SetAllDim();
		UpdateRoundLabel(round, totalRounds);
		SetStatus("Watch carefully...");
	}

	// =========================================================================
	// Visual helpers
	// =========================================================================
	private void SetButtonLit(int index)
	{
		_buttons[index].Modulate = ActiveColors[index];
	}

	private void SetButtonDim(int index)
	{
		_buttons[index].Modulate = DimColors[index];
	}

	private void SetAllDim()
	{
		for (int i = 0; i < _buttons.Length; i++)
			SetButtonDim(i);
	}

	private void SetInteractable(bool enabled)
	{
		foreach (var btn in _buttons)
			btn.Disabled = !enabled;
	}

	private void UpdateRoundLabel(int round, int total)
	{
		if (_roundLabel != null)
			_roundLabel.Text = $"Round : {round} / {total}";
	}

	private void SetStatus(string msg)
	{
		if (_statusLabel != null)
			_statusLabel.Text = msg;
	}
}
