using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct HitTargetStaticData : IComponentData
	{
		public int TargetType;
		public float DropSpeed;
		public float RaiseDelay;
		public float RotZ;
		public bool UseHitEvent;

		// table data
		public float TableScaleZ;

		public bool IsDropTarget => TargetType == Engine.VPT.TargetType.DropTargetBeveled
				|| TargetType == Engine.VPT.TargetType.DropTargetFlatSimple
				|| TargetType == Engine.VPT.TargetType.DropTargetSimple;
	}
}
