#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Flipper
{
	[RequiresEntityConversion]
	[AddComponentMenu("Visual Pinball/Flipper")]
	public class FlipperBehavior : ItemBehavior<Engine.VPT.Flipper.Flipper, FlipperData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{ FlipperMeshGenerator.BaseName, FlipperMeshGenerator.RubberName };

		protected override Engine.VPT.Flipper.Flipper GetItem()
		{
			return new Engine.VPT.Flipper.Flipper(data);
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var d = GetMaterialData();
			dstManager.AddComponentData(entity, d);
			dstManager.AddComponentData(entity, GetMovementData(d));
			dstManager.AddComponentData(entity, GetVelocityData(d));
			dstManager.AddComponentData(entity, new SolenoidStateData { Value = false });
			dstManager.AddComponentData(entity, new FlipperHitData());

			// register
			transform.GetComponentInParent<Player>().RegisterFlipper(Item, entity, gameObject);
		}

		private void Awake()
		{
			var rootObj = gameObject.transform.GetComponentInParent<TableBehavior>();
			// can be null in editor, shouldn't be at runtime.
			if (rootObj != null)
			{
				_tableData = rootObj.data;
			}
		}

		private FlipperMaterialData GetMaterialData()
		{
			float flipperRadius;
			if (data.FlipperRadiusMin > 0 && data.FlipperRadiusMax > data.FlipperRadiusMin) {
				flipperRadius = data.FlipperRadiusMax - (data.FlipperRadiusMax - data.FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				flipperRadius = math.max(flipperRadius, data.BaseRadius - data.EndRadius + 0.05f);

			} else {
				flipperRadius = data.FlipperRadiusMax;
			}

			flipperRadius = math.max(flipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			var angleStart = math.radians(data.StartAngle);
			var angleEnd = math.radians(data.EndAngle);

			if (angleEnd == angleStart) {
				// otherwise hangs forever in collisions/updates
				angleEnd += 0.0001f;
			}

			// model inertia of flipper as that of rod of length flipper around its end
			var mass = data.GetFlipperMass(_tableData);
			var inertia = (float) (1.0 / 3.0) * mass * (flipperRadius * flipperRadius);

			return new FlipperMaterialData {
				Inertia = inertia,
				AngleStart = angleStart,
				AngleEnd = angleEnd,
				Strength = data.GetStrength(_tableData),
				ReturnRatio = data.GetReturnRatio(_tableData),
				TorqueDamping = data.GetTorqueDamping(_tableData),
				TorqueDampingAngle = data.GetTorqueDampingAngle(_tableData),
				RampUpSpeed = data.GetRampUpSpeed(_tableData)
			};
		}

		private FlipperMovementData GetMovementData(FlipperMaterialData d)
		{
			// store flipper base rotation without starting angle
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -d.AngleStart)
			));
			return new FlipperMovementData {
				Angle = d.AngleStart,
				AngleSpeed = 0f,
				AngularMomentum = 0f,
				EnableRotateEvent = 0,
				BaseRotation = baseRotation,
			};
		}

		private static FlipperVelocityData GetVelocityData(FlipperMaterialData d)
		{
			return new FlipperVelocityData {
				AngularAcceleration = 0f,
				ContactTorque = 0f,
				CurrentTorque = 0f,
				Direction = d.AngleEnd >= d.AngleStart,
				IsInContact = false
			};
		}

		protected virtual void OnDrawGizmos()
		{
			// flippers tend to have sub object meshes, so nothing would be pickable on this game object,
			// but generally you'll want to manipulate the whole flipper, so we'll draw an invisible
			// gizmo slightly larger than one of the child meshes so clicking on the flipper in editor
			// selects this object
			var mf = this.GetComponentInChildren<MeshFilter>();
			if (mf != null && mf.sharedMesh != null) {
				Gizmos.color = Color.clear;
				Gizmos.DrawMesh(mf.sharedMesh, transform.position, transform.rotation, transform.lossyScale * 1.1f);
			}
		}
	}
}
