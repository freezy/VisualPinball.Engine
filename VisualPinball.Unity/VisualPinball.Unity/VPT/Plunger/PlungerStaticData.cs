using Unity.Entities;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerStaticData : IComponentData
	{
		// general
		public Entity RodEntity;
		public Entity SpringEntity;

		// collision
		public float MomentumXfer;
		public float ScatterVelocity;

		// displacement
		public float FrameStart;
		public float FrameEnd;
		public float FrameLen;
		public float RestPosition;

		// velocity
		public bool IsAutoPlunger;
		public float SpeedFire;

		// mesh frame calc
		public int NumFrames;
	}
}
