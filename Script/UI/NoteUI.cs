using Godot;

public partial class NoteUI : Control
{
	public static NoteUI Instance { get; private set; }
	[Export] private RichTextLabel _body;
	[Export] private Button closeButton;
	private bool _isOpen = false;

	public override void _EnterTree() => Instance = this;
	public override void _ExitTree() { if (Instance == this) Instance = null; }

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		closeButton.Pressed += CloseNote;
	}

	public void ShowNote(string content)
	{
		_body.Text = content;
		Visible = true;
		_isOpen = true;
		GetTree().Paused = true;
		GD.Print($"Note trigger with content : {content}");
	}

	public void CloseNote()
	{
		Visible = false;
		_isOpen = false;
		GetTree().Paused = false;
		GD.Print("Close the panel");
	}
}
