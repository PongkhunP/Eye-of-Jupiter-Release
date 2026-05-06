using Godot;

public sealed class BehaviorTreeRunner
{
	private readonly BTNode _root;
	private readonly BTBlackboard _blackboard;

	public BehaviorTreeRunner(BTNode root, BTBlackboard blackboard)
	{
		_root = root;
		_blackboard = blackboard;
	}

	public BTState Tick(Node owner, double delta)
	{
		return _root.Tick(owner, _blackboard, delta);
	}
}
