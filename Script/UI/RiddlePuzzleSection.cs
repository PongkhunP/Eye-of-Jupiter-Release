using Godot;
using System;

public partial class RiddlePuzzleSection : Control
{
	[Export] private RichTextLabel _questionLabel;
	[Export] private Label         _hintLabel;
	[Export] private LineEdit      _answerInput;
	[Export] private Button        _submitButton;
	[Export] private Label         _feedbackLabel;
 
	public override void _Ready()
	{
		_submitButton.Pressed      += OnSubmitPressed;
		_answerInput.TextSubmitted += _ => OnSubmitPressed();
	}
 
	/// <summary>Called by PuzzleUi when this section becomes active.</summary>
	public void Populate(string question, string hint)
	{
		// RichTextLabel supports BBCode — wrap in italics for flavour
		_questionLabel.Text = $"[i]{question}[/i]";
		_hintLabel.Text     = hint;
		_answerInput.Clear();
		_feedbackLabel.Text = "";
		_answerInput.GrabFocus();
	}
 
	private void OnSubmitPressed()
	{
		string raw = _answerInput.Text.Trim();
		if (string.IsNullOrEmpty(raw)) return;
 
		bool correct = PuzzleManager.Instance.TrySubmitAnswer(raw);
 
		if (!correct)
		{
			_feedbackLabel.Text = "✗  The gods are not satisfied — think again.";
			_answerInput.Clear();
			_answerInput.GrabFocus();
		}
	}
}
