using Godot;

public abstract class BTNode
{
	public abstract BTState Tick(Node owner, BTBlackboard blackboard, double delta);
}
