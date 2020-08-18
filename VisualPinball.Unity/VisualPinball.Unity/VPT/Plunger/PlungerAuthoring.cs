using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Plunger")]
	public class PlungerAuthoring : ItemAuthoring<Plunger, PlungerData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new [] {
			PlungerMeshGenerator.FlatName, PlungerMeshGenerator.RodName, PlungerMeshGenerator.SpringName
		};

		protected override Plunger GetItem() => new Plunger(data);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Plunger>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPlunger(Item, entity, gameObject);

			Item.Init(table);
			var hit = Item.PlungerHit;
			hit.SetIndex(entity.Index, entity.Version);

			dstManager.AddComponentData(entity, new PlungerStaticData {
				MomentumXfer = data.MomentumXfer,
				ScatterVelocity = data.ScatterVelocity,
				FrameStart = hit.FrameBottom,
				FrameEnd = hit.FrameTop,
				FrameLen = hit.FrameLen,
				RestPosition = hit.RestPos,
				IsAutoPlunger = data.AutoPlunger,
				SpeedFire = data.SpeedFire,
				NumFrames = Item.MeshGenerator.NumFrames
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

			dstManager.AddComponentData(entity, new PlungerVelocityData {
				Mech0 = 0f,
				Mech1 = 0f,
				Mech2 = 0f,
				PullForce = 0f,
				InitialSpeed = 0f,
				AutoFireTimer = 0,
				AddRetractMotion = false,
				RetractWaitLoop = 0,
				MechStrength = data.MechStrength
			});
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex3D();
	}
}
