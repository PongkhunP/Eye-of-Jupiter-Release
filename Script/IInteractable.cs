/// <summary>
/// Nodes that respond when the player presses interact in range (e.g. shrines, NPCs).
/// </summary>
public interface IInteractable
{
	void Interact(PlayerController player);
}
