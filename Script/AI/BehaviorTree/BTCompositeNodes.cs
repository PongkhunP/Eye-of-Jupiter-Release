using Godot;
using System.Collections.Generic;

public sealed class SelectorNode : BTNode
{
	private readonly List<BTNode> _children;

	public SelectorNode(params BTNode[] children)
	{
		_children = new List<BTNode>(children);
	}

	public override BTState Tick(Node owner, BTBlackboard blackboard, double delta)
	{
		foreach (BTNode child in _children)
		{
			BTState result = child.Tick(owner, blackboard, delta);
			if (result != BTState.Failure)
				return result;
		}

		return BTState.Failure;
	}
}

public sealed class SequenceNode : BTNode
{
	private readonly List<BTNode> _children;

	public SequenceNode(params BTNode[] children)
	{
		_children = new List<BTNode>(children);
	}

	public override BTState Tick(Node owner, BTBlackboard blackboard, double delta)
	{
		foreach (BTNode child in _children)
		{
			BTState result = child.Tick(owner, blackboard, delta);
			if (result != BTState.Success)
				return result;
		}

		return BTState.Success;
	}
}
