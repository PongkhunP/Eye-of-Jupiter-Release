using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload: Hermes / Poseidon (and more) dialogue lines; blocks player movement while active.
/// </summary>
public partial class DialogueManager : Node
{
	public const string DialogueUiScenePath = "res://UI/DialogueUI.tscn";
	public const string DialogueDataPath = "res://Data/dialogue.json";

	public static DialogueManager Instance { get; private set; }

	public bool IsDialogueActive { get; private set; }

	[Signal]
	public delegate void DialogueLineChangedEventHandler(string text);

	[Signal]
	public delegate void DialogueEndedEventHandler();

	private readonly Dictionary<string, List<string>> _linesByNpc = new();

	private DialogueUi _ui;
	private string _currentNpcId;
	private int _lineIndex;
	private List<string> _currentLines;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		LoadDialogueData();
		CallDeferred(nameof(EnsureUi));
	}

	public override void _Process(double delta)
	{
		if (!IsDialogueActive)
			return;

		if (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("ui_accept"))
			AdvanceDialogue();
	}

	private void EnsureUi()
	{
		if (_ui != null)
			return;

		if (!ResourceLoader.Exists(DialogueUiScenePath))
		{
			GD.PushWarning($"{nameof(DialogueManager)}: missing {DialogueUiScenePath}");
			return;
		}

		var scene = GD.Load<PackedScene>(DialogueUiScenePath);
		_ui = scene.Instantiate<DialogueUi>();
		GetTree().Root.AddChild(_ui);
		_ui.HideDialogue();
	}

	private void LoadDialogueData()
	{
		_linesByNpc.Clear();

		if (!FileAccess.FileExists(DialogueDataPath))
		{
			SeedFallbackLines();
			return;
		}

		using var file = FileAccess.Open(DialogueDataPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			SeedFallbackLines();
			return;
		}

		var json = file.GetAsText();
		var variant = Json.ParseString(json);
		if (variant.VariantType != Variant.Type.Dictionary)
		{
			SeedFallbackLines();
			return;
		}

		var root = variant.AsGodotDictionary();
		foreach (var key in root.Keys)
		{
			var npcId = key.AsString().ToLowerInvariant();
			var arr = root[key].AsGodotArray();
			var list = new List<string>();
			foreach (var line in arr)
				list.Add(line.AsString());
			_linesByNpc[npcId] = list;
		}
	}

	private void SeedFallbackLines()
	{
		_linesByNpc["hermes"] = new List<string>
		{
			"Hermes: Zeus has chained the storm to your breath — every second steals more air.",
			"Hermes: Three shrines must answer before the pod will wake. I will not repeat myself."
		};
		_linesByNpc["poseidon"] = new List<string>
		{
			"Poseidon: The clouds here drink oxygen like the sea drinks rivers. Move quickly."
		};
	}

	/// <summary>Begin dialogue for an NPC id present in dialogue.json (or fallback).</summary>
	public void StartDialogue(string npcId)
	{
		EnsureUi();
		if (_ui == null)
			return;

		if (!_linesByNpc.TryGetValue(npcId.ToLowerInvariant(), out var lines) || lines.Count == 0)
		{
			GD.PushWarning($"{nameof(DialogueManager)}: no lines for '{npcId}'");
			return;
		}

		_currentNpcId = npcId;
		_currentLines = lines;
		_lineIndex = 0;
		IsDialogueActive = true;
		ShowCurrentLine();
	}

	private void ShowCurrentLine()
	{
		string text = _currentLines[_lineIndex];
		_ui.ShowLine(text);
		EmitSignal(SignalName.DialogueLineChanged, text);
	}

	private void AdvanceDialogue()
	{
		if (!IsDialogueActive || _currentLines == null)
			return;

		_lineIndex++;
		if (_lineIndex >= _currentLines.Count)
		{
			EndDialogue();
			return;
		}

		ShowCurrentLine();
	}

	public void EndDialogue()
	{
		IsDialogueActive = false;
		_currentLines = null;
		_currentNpcId = null;
		_lineIndex = 0;

		if (_ui != null)
			_ui.HideDialogue();

		EmitSignal(SignalName.DialogueEnded);
	}
}
