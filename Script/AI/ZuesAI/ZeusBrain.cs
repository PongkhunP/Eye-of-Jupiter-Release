using System.Diagnostics;
using Godot;


public partial class ZeusBrain : Node2D
{
    // ── Exports ───────────────────────────────────────────────────
    [Export] public float MaxHealth { get; set; } = 220f;
    [Export] public float BoltDamage { get; set; } = 12f;
    [Export] public float SmiteDamage { get; set; } = 24f;
    [Export] public float RoarKnockbackForce { get; set; } = 2400f;
    [Export] public float RoarStunDuration { get; set; } = 2.0f;
    [Export] public float RoarCooldownSeconds { get; set; } = 12f;
    [Export] public float OrbCooldownSeconds { get; set; } = 3.5f;
    [Export] public float SpearCooldownSeconds { get; set; } = 4.0f;
    [Export] public float BoltCooldownSeconds { get; set; } = 2.0f;
    [Export] public float SmiteCooldownSeconds { get; set; } = 5.0f;
    [Export] public float AreaDenialCooldownSeconds { get; set; } = 7.0f;
    [Export] public float OrbSpeed { get; set; } = 80f;
    [Export] public float OrbLifetime { get; set; } = 4.0f;
    [Export] public float OrbDamage { get; set; } = 10f;
    [Export] public float SpearWarningDuration { get; set; } = 1.2f;
    [Export] public float SpearDamage { get; set; } = 30f;
    [Export] public float RiddleDamagePerHit { get; set; } = 40f;
    [Export] public float PreferredDistance { get; set; } = 250f;
    [Export] public float DetectionRadius { get; set; } = 500f;
    [Export] public bool ShowZapVfx { get; set; } = true;
    [Export] public float ZapVfxDurationSeconds { get; set; } = 0.15f;
    [Export] public float ZapVfxWidth { get; set; } = 4.0f;
    [Export] public Area2D interactArea { get; set; }
    [Export] public float MinCastDelay { get; set; } = 2f;
    [Export] public float MaxCastDelay { get; set; } = 5f;
    [Export] public float TeleportCooldown { get; set; } = 6f;

    [Export] public Node2D zeusBody { get; set; }
    [Export] public ZeusBodyController zeusBodyController { get; set; }

    // ── State ─────────────────────────────────────────────────────
    private float _castDelayCd = 0f; // global delay between ANY cast
    private float _teleportCd = 0f;
    internal bool _isCasting = false;
    internal bool _isStunned = false;
    internal float _health;
    internal bool _dead;
    internal int _phase = 1;
    internal bool _openingRoarDone = false;
    internal bool _playerInRange = false;

    internal float _roarCd;
    internal float _orbCd;
    internal float _spearCd;
    internal float _boltCd;
    internal float _smiteCd;
    internal float _denialCd;
    internal float _puzzleFailPunishLeft;

    internal const string ZeusRiddleShrineId = "zeus_shrine";

    private BehaviorTreeRunner _tree;
    internal readonly BTBlackboard _blackboard = new();
    public BTBlackboard Blackboard => _blackboard;

    // Cast quota per phase
    private int _castsThisRound = 0;
    private int _castQuota => _phase == 1 || _phase == 2 ? 1 : 2; // phase 1=1 cast, phase 2=2 casts, phase 3=3 casts

    private bool CanCast() => _castsThisRound < _castQuota && _castDelayCd <= 0f;
    private void RegisterCast()
    {
        _castsThisRound++;
        GD.Print($"[Zeus] Cast {_castsThisRound}/{_castQuota} this round");

        if (_castsThisRound >= _castQuota)
        {
            // Quota reached — start delay then reset
            StartCastDelay();
            GD.Print($"[Zeus] Quota reached — resting {_castDelayCd:0.0}s");
        }
    }


    // ── Lifecycle ─────────────────────────────────────────────────
    public override void _Ready()
    {
        _health = MaxHealth;
        _roarCd = 0f;
        _orbCd = OrbCooldownSeconds;
        _spearCd = SpearCooldownSeconds;
        _boltCd = BoltCooldownSeconds;
        _smiteCd = SmiteCooldownSeconds;
        _denialCd = AreaDenialCooldownSeconds;

        GD.Print($"Interact area found ? : {interactArea != null}");
        if (interactArea != null)
        {
            interactArea.BodyEntered += (body) => { if (body.IsInGroup("player")) { GD.Print("Player Dtect"); _playerInRange = true; } };
            interactArea.BodyExited += (body) => { if (body.IsInGroup("player")) _playerInRange = false; };
        }

        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.TrialFailed += OnRiddleWrong;
            PuzzleManager.Instance.TrialCompleted += OnRiddleCorrect;
        }

        BuildTree();
    }

    public override void _ExitTree()
    {
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.TrialFailed -= OnRiddleWrong;
            PuzzleManager.Instance.TrialCompleted -= OnRiddleCorrect;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;


        // ── Freeze everything while riddle is active ──
        if (PuzzleManager.Instance.IsPuzzleActive)
        {
            // Keep blackboard updated so body controller stops
            _blackboard.Set("is_casting", true);
            _blackboard.Set("is_stunned", true);
            return; // skip BT and cooldown ticks
        }

        float dt = (float)delta;
        _castDelayCd = Mathf.Max(0f, _castDelayCd - dt);
        _teleportCd = Mathf.Max(0f, _teleportCd - dt);
        _roarCd = Mathf.Max(0f, _roarCd - dt);
        _orbCd = Mathf.Max(0f, _orbCd - dt);
        _spearCd = Mathf.Max(0f, _spearCd - dt);
        _boltCd = Mathf.Max(0f, _boltCd - dt);
        _smiteCd = Mathf.Max(0f, _smiteCd - dt);
        _denialCd = Mathf.Max(0f, _denialCd - dt);
        _puzzleFailPunishLeft = Mathf.Max(0f, _puzzleFailPunishLeft - dt);


        bool isVulnerable = _blackboard.Get<bool>("is_vulnerable");

        if (_playerInRange && isVulnerable
            && Input.IsActionJustPressed("interact")
            && !PuzzleManager.Instance.IsPuzzleActive)
        {
            GD.Print("[Zeus] Starting riddle!");
            PuzzleManager.Instance.StartRiddlePuzzle(ZeusRiddleShrineId);
        }

        UpdatePhaseAndBlackboard();
        _tree.Tick(this, delta);
    }

    public void TakeDamage(float amount)
    {
        if (_dead || amount <= 0f) return;
        _health = Mathf.Max(0f, _health - amount);
        GD.Print($"[Zeus] TakeDamage {amount} → HP {_health}");
        if (_health <= 0f) _dead = true;
    }

    internal PlayerController ResolvePlayer()
    {
        if (PlayerController.Instance != null) return PlayerController.Instance;
        foreach (var node in GetTree().GetNodesInGroup("player"))
            if (node is PlayerController pc) return pc;
        return null;
    }

    private void BuildTree()
    {
        _tree = new BehaviorTreeRunner(
            new SelectorNode(
                // Dead
                new SequenceNode(
                    new ConditionNode((o, bb, d) => _dead),
                    new ActionNode((o, bb, d) =>
                    {
                        SetPhysicsProcess(false);
                        return BTState.Success;
                    })
                ),
                // Opening roar
                new SequenceNode(
                    new ConditionNode((o, bb, d) => !_openingRoarDone && _roarCd <= 0f),
                    new ActionNode((o, bb, d) => { _openingRoarDone = true; return CastRoar(); })
                ),
                // Phase 3
                new SequenceNode(
                    new ConditionNode((o, bb, d) => _phase == 3),
                    new SelectorNode(
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _spearCd <= 0f), new ActionNode((o, bb, d) => CastLightningSpear())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _orbCd   <= 0f), new ActionNode((o, bb, d) => SpawnOrbRing())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _roarCd  <= 0f), new ActionNode((o, bb, d) => CastRoar())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _denialCd <= 0f), new ActionNode((o, bb, d) => SpawnAcidRainZone())),
                        new SequenceNode(new ConditionNode((o, bb, d) => !CanCast() && _teleportCd <= 0f), new ActionNode((o, bb, d) => Teleport())),
                        new SequenceNode(new ConditionNode((o, bb, d) => _puzzleFailPunishLeft > 0f), new ActionNode((o, bb, d) => CastPunishmentBolt())),
                        new ActionNode((o, bb, d) => IdleHover())
                    )
                ),
                // Phase 2
                new SequenceNode(
                    new ConditionNode((o, bb, d) => _phase == 2),
                    new SelectorNode(
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _spearCd  <= 0f), new ActionNode((o, bb, d) => CastLightningSpear())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _orbCd    <= 0f), new ActionNode((o, bb, d) => SpawnOrbRing())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _denialCd <= 0f), new ActionNode((o, bb, d) => SpawnAcidRainZone())),
                        new SequenceNode(new ConditionNode((o, bb, d) => !CanCast() && _teleportCd <= 0f), new ActionNode((o, bb, d) => Teleport())),
                        new ActionNode((o, bb, d) => IdleHover())
                    )
                ),
                // Phase 1
                new SequenceNode(
                    new ConditionNode((o, bb, d) => _phase == 1),
                    new SelectorNode(
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _orbCd <= 0f), new ActionNode((o, bb, d) => SpawnOrbRing())),
                        new SequenceNode(new ConditionNode((o, bb, d) => CanCast() && _roarCd <= 0f), new ActionNode((o, bb, d) => CastRoar())),
                        new ActionNode((o, bb, d) => IdleHover())
                    )
                ),

                new ActionNode((o, bb, d) => IdleHover())
            ),
            _blackboard
        );
    }

    private BTState Taunt() => BTState.Success;
    private BTState IdleHover() => BTState.Running;

    private void AdvancePhase()
    {
        _phase++;
        GD.Print($"[Zeus] Phase advanced to {_phase}");

        if (_phase > 3)
        {
            _dead = true;
            _phase = 3; // keep valid for blackboard
            GD.Print("[Zeus] Defeated!");
            UnlockEscapeFlow();
            return;
        }

        // Roar on every phase transition
        CastRoar();
        GD.Print($"[Zeus] Entering phase {_phase}!");
    }
}

