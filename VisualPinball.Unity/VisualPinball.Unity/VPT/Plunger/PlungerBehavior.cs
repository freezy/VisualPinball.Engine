using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collider;

namespace VisualPinball.Unity.VPT.Plunger
{
	[AddComponentMenu("Visual Pinball/Plunger")]
	public class PlungerBehavior : ItemBehavior<Engine.VPT.Plunger.Plunger, PlungerData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{ PlungerMeshGenerator.FlatName, PlungerMeshGenerator.RodName, PlungerMeshGenerator.SpringName };

		protected override Engine.VPT.Plunger.Plunger GetItem() => new Engine.VPT.Plunger.Plunger(data);

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var hit = Item.PlungerHit;

			dstManager.AddComponentData(entity, new PlungerStaticData {
				MomentumXfer = data.MomentumXfer,
				ScatterVelocity = data.ScatterVelocity,
				FrameStart = hit.FrameBottom,
				FrameEnd = hit.FrameTop,
				FrameLen = hit.FrameLen,
				RestPosition = hit.RestPos
			});

			dstManager.AddComponentData(entity, new PlungerColliderData {
				JointEnd0 = LineZCollider.Create(hit.JointEnd[0]),
				JointEnd1 = LineZCollider.Create(hit.JointEnd[1]),
				LineSegEnd = LineCollider.Create(hit.LineSegEnd),
				LineSegSide0 = LineCollider.Create(hit.LineSegSide[0]),
				LineSegSide1 = LineCollider.Create(hit.LineSegSide[1])
			});

			dstManager.AddComponentData(entity, new PlungerMovementData {
				FireBounce = 0f,
				Position = hit.Position,
				RetractMotion = false,
				ReverseImpulse = 0f,
				Speed = 0f,
				TravelLimit = hit.FrameTop,
				FireSpeed = 0f,
				FireTimer = 0
			});
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex3D();
	}
}
