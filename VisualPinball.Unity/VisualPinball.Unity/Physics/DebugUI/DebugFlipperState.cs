namespace VisualPinball.Unity.Physics.DebugUI
{
	public struct DebugFlipperState
	{
		public float Angle;
		public bool Solenoid;

		public DebugFlipperState(float angle, bool solenoid)
		{
			Angle = angle;
			Solenoid = solenoid;
		}
	}
}
