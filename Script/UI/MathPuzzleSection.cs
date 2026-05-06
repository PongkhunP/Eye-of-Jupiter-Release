using Godot;
using System;

public partial class MathPuzzleSection : Control
{
	[Export] private RichTextLabel _questionLabel;
	[Export] private Label         _hintLabel;
	[Export] private LineEdit      _answerInput;
	[Export] private Button        _submitButton;
	[Export] private Label         _feedbackLabel;
 
	public override void _Ready()
	{
		_submitButton.Pressed += OnSubmitPressed;
 
		// Allow pressing Enter to submit
		_answerInput.TextSubmitted += _ => OnSubmitPressed();
	}
 
	/// <summary>Called by PuzzleUi when this section becomes active.</summary>
	public void Populate(string question, string hint)
	{
		_questionLabel.Text  = question;
		_hintLabel.Text      = hint;
		_answerInput.Clear();
		_feedbackLabel.Text  = "";
		_answerInput.GrabFocus();
	}
 
	private void OnSubmitPressed()
	{
		string raw = _answerInput.Text.Trim();
		if (string.IsNullOrEmpty(raw)) return;
 
		bool correct = PuzzleManager.Instance.TrySubmitAnswer(raw);
 
		if (!correct)
		{
			_feedbackLabel.Text = "✗  Wrong answer — try again!";
			_answerInput.Clear();
			_answerInput.GrabFocus();
		}
		// On correct, PuzzleManager emits TrialCompleted → PuzzleUi hides everything
	}
}
