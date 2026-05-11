using Godot;

public partial class LockPuzzleSection : Control
{
	// [Export] private RichTextLabel _questionLabel;
	// [Export] private Label _hintLabel;

	// The "Hello" label that shows the current input
	[Export] private Label _inputDisplayLabel;

	[Export] private Button _delButton;
	[Export] private Button _submitButton;
	[Export] private Label _feedbackLabel;
	[Export] private Button _closeButton;

	// Digit buttons: assign Button-1 through Button-9 in the Inspector
	[Export] private Button[] _digitButtons = new Button[9];

	private int _digits = 4;
	private string _currentInput = "";

	public override void _Ready()
	{
		_submitButton.Pressed += OnSubmitPressed;
		_delButton.Pressed += OnDelPressed;
		_closeButton.Pressed += OnClosePressed;

		for (int i = 0; i < _digitButtons.Length; i++)
		{
			int digit = i + 1; // buttons are 1–9
			if(digit == 10) digit = 0;
			_digitButtons[i].Pressed += () => OnDigitPressed(digit);
		}
	}

	private void OnClosePressed()
	{
		PuzzleManager.Instance.CancelPuzzle();  // stops timer, clears state — no signals emitted
		_currentInput = "";
		UpdateDisplay();
		_feedbackLabel.Text = "";
		Visible = false;

		GetOwner<PuzzleUI>()?.HideAll();
	}

	public void Populate(string question, string hint, int digits)
	{
		_digits = Mathf.Clamp(digits, 1, 12);
		// _questionLabel.Text = question;
		// _hintLabel.Text = $"{hint} (length: {_digits})";
		_feedbackLabel.Text = "";
		_currentInput = "";
		UpdateDisplay();
	}

	private void OnDigitPressed(int digit)
	{
		if (_currentInput.Length >= _digits)
			return;

		_currentInput += digit.ToString();
		UpdateDisplay();
	}

	private void OnDelPressed()
	{
		if (_currentInput.Length == 0)
			return;

		_currentInput = _currentInput[..^1];
		UpdateDisplay();
	}

	private void OnSubmitPressed()
	{
		if (_currentInput.Length != _digits)
		{
			_feedbackLabel.Text = $"Enter exactly {_digits} digits.";
			return;
		}

		bool correct = PuzzleManager.Instance.TrySubmitAnswer(_currentInput);
		if (!correct)
		{
			_feedbackLabel.Text = "LOCKED. Incorrect code.";
			_currentInput = "";
			UpdateDisplay();
		}
	}

	private void UpdateDisplay()
	{
		// Show entered digits, pad the rest with underscores
		string displayed = _currentInput.PadRight(_digits, '_');
		_inputDisplayLabel.Text = displayed;
	}
}

