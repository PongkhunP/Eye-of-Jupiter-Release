using Godot;
using System;

public sealed class ConditionNode : BTNode
{
	private readonly Func<Node, BTBlackboard, double, bool> _predicate;

	public ConditionNode(Func<Node, BTBlackboard, double, bool> predicate)
	{
		_predicate = predicate;
	}

	public override BTState Tick(Node owner, BTBlackboard blackboard, double delta)
	{
		return _predicate(owner, blackboard, delta) ? BTState.Success : BTState.Failure;
	}
}

public sealed class ActionNode : BTNode
{
	private readonly Func<Node, BTBlackboard, double, BTState> _action;

	public ActionNode(Func<Node, BTBlackboard, double, BTState> action)
	{
		_action = action;
	}

	public override BTState Tick(Node owner, BTBlackboard blackboard, double delta)
	{
		return _action(owner, blackboard, delta);
	}
}
