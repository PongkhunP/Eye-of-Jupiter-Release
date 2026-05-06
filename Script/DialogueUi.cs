using Godot;

/// <summary>
/// Bottom panel UI for NPC lore; visibility toggled by DialogueManager.
/// </summary>
public partial class DialogueUi : Control
{
	private RichTextLabel _body;
	private Label _hint;

	public override void _Ready()
	{
		_body = GetNode<RichTextLabel>("Panel/MarginContainer/VBox/RichTextLabel");
		_hint = GetNode<Label>("Panel/MarginContainer/VBox/HintLabel");
		HideDialogue();
	}

	public void ShowLine(string text, string hint = "E — continue")
	{
		_body.Text = text;
		_hint.Text = hint;
		Visible = true;
	}

	public void HideDialogue()
	{
		Visible = false;
	}
}
