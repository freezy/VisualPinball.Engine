#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Flipper;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Flipper")]
	public class VisualPinballFlipper : ItemComponent<Flipper, FlipperData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{ FlipperMeshGenerator.BaseName, FlipperMeshGenerator.RubberName };

		protected override Flipper GetItem()
		{
			return new Flipper(data);
		}

		public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
		{
			var tp = transform.GetComponentInParent<TablePlayer>().FlipperEntities[data.Name] = entity;
			var d = GetMaterialData();
			manager.AddComponentData(entity, d);
			manager.AddComponentData(entity, GetMovementData(d));
			manager.AddComponentData(entity, GetVelocityData(d));
			manager.AddComponentData(entity, new SolenoidStateData { Value = false });
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
				BaseRotation = baseRotation
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
	}
}
