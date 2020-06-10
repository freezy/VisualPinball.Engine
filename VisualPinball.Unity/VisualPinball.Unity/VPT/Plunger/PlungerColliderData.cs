using Unity.Entities;
using VisualPinball.Unity.Physics.Collider;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerColliderData : IComponentData
	{
		public LineCollider LineSegSide0;
		public LineCollider LineSegSide1;
		public LineZCollider JointEnd0;
		public LineZCollider JointEnd1;
	}
}
