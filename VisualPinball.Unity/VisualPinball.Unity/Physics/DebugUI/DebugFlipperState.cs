using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct DebugFlipperState
	{
		public Entity Entity;
		public float Angle;
		public bool Solenoid;

		public DebugFlipperState(Entity entity, float angle, bool solenoid)
		{
			Entity = entity;
			Angle = angle;
			Solenoid = solenoid;
		}
	}
}
