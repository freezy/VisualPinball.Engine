namespace VisualPinball.Unity.Physics.Collider
{
	public interface ICollider
	{
		ColliderType Type { get; }

		int MemorySize { get; }
	}
}
