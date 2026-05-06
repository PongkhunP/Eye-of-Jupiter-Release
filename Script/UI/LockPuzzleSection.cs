using Godot;

public partial class LockPuzzleSection : Control
{
	[Export] private RichTextLabel _questionLabel;
	[Export] private Label _hintLabel;
	[Export] private LineEdit _codeInput;
	[Export] private Button _submitButton;
	[Export] private Label _feedbackLabel;

	private int _digits = 4;

	public override void _Ready()
	{
		_submitButton.Pressed += OnSubmitPressed;
		_codeInput.TextSubmitted += _ => OnSubmitPressed();
		_codeInput.TextChanged += OnCodeTextChanged;
	}

	public void Populate(string question, string hint, int digits)
	{
		_digits = Mathf.Clamp(digits, 1, 12);
		_questionLabel.Text = question;
		_hintLabel.Text = $"{hint} (length: {_digits})";
		_feedbackLabel.Text = "";
		_codeInput.Clear();
		_codeInput.MaxLength = _digits;
		_codeInput.PlaceholderText = new string('_', _digits);
		_codeInput.GrabFocus();
	}

	private void OnCodeTextChanged(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		char[] filtered = new char[text.Length];
		int idx = 0;
		foreach (char c in text)
		{
			if (char.IsDigit(c))
				filtered[idx++] = c;
		}

		string numeric = new string(filtered, 0, idx);
		if (numeric.Length > _digits)
			numeric = numeric[.._digits];

		if (numeric != text)
		{
			int caret = Mathf.Min(numeric.Length, _codeInput.CaretColumn);
			_codeInput.Text = numeric;
			_codeInput.CaretColumn = caret;
		}
	}

	private void OnSubmitPressed()
	{
		string raw = _codeInput.Text.Trim();
		if (raw.Length != _digits)
		{
			_feedbackLabel.Text = $"Enter exactly {_digits} digits.";
			_codeInput.GrabFocus();
			return;
		}

		bool correct = PuzzleManager.Instance.TrySubmitAnswer(raw);
		if (!correct)
		{
			_feedbackLabel.Text = "LOCKED. Incorrect code.";
			_codeInput.Clear();
			_codeInput.GrabFocus();
		}
	}
}

