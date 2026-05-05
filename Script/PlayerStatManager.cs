using Godot;

public partial class PlayerStatManager : Node
{
	public static PlayerStatManager Instance { get; private set; }

	// ── Exports ───────────────────────────────────────────────────────────────

	[Export] public float MaxO2 { get; set; } = 100f;
	[Export] public float MaxHp { get; set; } = 100f;

	/// <summary>Baseline O2 drain in the Jupiter atmosphere (units / second).</summary>
	[Export] public float BaseO2DrainPerSecond { get; set; } = 2f;

	// ── State ─────────────────────────────────────────────────────────────────

	public float O2 { get; private set; }
	public float Hp { get; private set; }
	public bool IsDead { get; private set; }

	// ── Signals ───────────────────────────────────────────────────────────────

	[Signal] public delegate void O2ChangedEventHandler(float current, float max);
	[Signal] public delegate void HpChangedEventHandler(float current, float max);
	[Signal] public delegate void PlayerDiedEventHandler();

	// ── Cache to suppress redundant signal emissions ──────────────────────────

	private float _lastEmittedO2 = float.NaN;
	private float _lastEmittedHp = float.NaN;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _EnterTree() => Instance = this;

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Ready()
	{
		O2 = MaxO2;
		Hp = MaxHp;
		EmitO2IfChanged();
		EmitHpIfChanged();
	}

	public void Tick(float delta, float extraO2Drain = 0f, float extraHpDrain = 0f)
	{
		if (IsDead)
			return;

		float totalO2Drain = (BaseO2DrainPerSecond + extraO2Drain) * delta;
		float totalHpDrain = extraHpDrain * delta;

		if (totalO2Drain > 0f)
			ModifyO2(-totalO2Drain);

		if (totalHpDrain > 0f)
			ModifyHp(-totalHpDrain);
	}

	public void TakeDamage(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		ModifyHp(-amount);
	}

	public void RestoreO2(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		ModifyO2(amount);
	}
	public void Heal(float amount)
	{
		if (IsDead || amount <= 0f)
			return;

		ModifyHp(amount);
	}

	private void ModifyO2(float delta)
	{
		O2 = Mathf.Clamp(O2 + delta, 0f, MaxO2);
		EmitO2IfChanged();

		if (!IsDead && O2 <= 0f)
			Die();
	}

	private void ModifyHp(float delta)
	{
		Hp = Mathf.Clamp(Hp + delta, 0f, MaxHp);
		EmitHpIfChanged();

		if (!IsDead && Hp <= 0f)
			Die();
	}

	private void Die()
	{
		if (IsDead)
			return;

		IsDead = true;
		EmitSignal(SignalName.PlayerDied);
	}

	private void EmitO2IfChanged()
	{
		if (float.IsNaN(_lastEmittedO2) || Mathf.Abs(_lastEmittedO2 - O2) > 0.001f)
		{
			_lastEmittedO2 = O2;
			EmitSignal(SignalName.O2Changed, O2, MaxO2);
		}
	}

    private void EmitHpIfChanged()
	{
		if (float.IsNaN(_lastEmittedHp) || Mathf.Abs(_lastEmittedHp - Hp) > 0.001f)
		{
			_lastEmittedHp = Hp;
			EmitSignal(SignalName.HpChanged, Hp, MaxHp);
		}
	}
}
