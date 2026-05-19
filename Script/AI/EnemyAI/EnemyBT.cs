using Godot;
using System;

public partial class EnemyBrain
{
    private void BuildTree()
    {
        _tree = new BehaviorTreeRunner(
            new SelectorNode(

                // ── Dead ─────────────────────────────────────────
                new SequenceNode(
                    new ConditionNode((o, bb, d) => _dead),
                    new ActionNode((o, bb, d) => HandleDeath())
                ),
                new SequenceNode(
                    new ConditionNode((o, bb, d) => bb.GetOrDefault<bool>("can_see_player")),
                    new SelectorNode(
                        // In attack range + cooldown ready → attack
                        new SequenceNode(
                            new ConditionNode((o, bb, d) =>
                                bb.GetOrDefault<float>("distance_to_player") <= AttackRange),
                            new ConditionNode((o, bb, d) => _cooldownLeft <= 0f),
                            new ActionNode((o, bb, d) => AttackPlayer())
                        ),
                        // In attack range but on cooldown → idle in place
                        new SequenceNode(
                            new ConditionNode((o, bb, d) =>
                                bb.GetOrDefault<float>("distance_to_player") <= AttackRange),
                            new ActionNode((o, bb, d) =>
                            {
                                Velocity = Vector2.Zero;
                                PlayAnim("idle");
                                return BTState.Running;
                            })
                        ),
                        // Chase player
                        new ActionNode((o, bb, d) => ChasePlayer((float)d))
                    )
                ),

                // ── Investigate last known position ───────────────
                new SequenceNode(
                    new ConditionNode((o, bb, d) =>
                        _blackboard.TryGet<Vector2>("last_known_pos", out _)
                        && _investigateLeft > 0f),
                    new ActionNode((o, bb, d) => Investigate((float)d))
                ),

                // ── Patrol ────────────────────────────────────────
                new ActionNode((o, bb, d) => Patrol((float)d))
            ),
            _blackboard
        );
    }

    private void UpdateBlackboard()
    {
        var player = ResolvePlayer();
        if (player == null)
        {
            _blackboard.Set("can_see_player", false);
            return;
        }

        float dist   = GlobalPosition.DistanceTo(player.GlobalPosition);
        bool  canSee = dist <= DetectionRange && HasLineOfSight(player.GlobalPosition);

        _blackboard.Set("distance_to_player", dist);
        _blackboard.Set("can_see_player",     canSee);

        if (canSee)
        {
            _blackboard.Set("last_known_pos", player.GlobalPosition);
            _investigateLeft = InvestigateTime;
        }
        else if (dist > LoseAggroRange)
        {
            // Lost player completely — clear last known
            _blackboard.Set("last_known_pos", GlobalPosition);
            _investigateLeft = 0f;
        }
    }

    private bool HasLineOfSight(Vector2 targetPos)
    {
        var space  = GetWorld2D().DirectSpaceState;
        var query  = PhysicsRayQueryParameters2D.Create(
            GlobalPosition, targetPos,
            collisionMask: 1 // world layer only
        );
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = space.IntersectRay(query);
        return result.Count == 0; // no wall in the way
    }
}