#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Transforms;
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

		public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
		{
			var d = GetMaterialData();
			manager.AddComponentData(entity, d);
			manager.AddComponentData(entity, GetMovementData(d));
			manager.AddComponentData(entity, GetVelocityData(d));
			manager.AddComponentData(entity, new SolenoidStateData { Value = false });
			AddMissingECSComponents(entity, manager, conversionSystem);

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
			AddMissingPhysicsComponents();
		}

		private void AddMissingECSComponents(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
		{
			var d = GetMaterialData();
			var surfaceWithFlipper = gameObject.transform.GetComponentInParent<TableBehavior>(); // Now it is table... but it may be surface with flipper too.

			//PhysicsJoint
			var flipperRotationFix = new Quaternion(0.7071067811865475f, 0, 0.7071067811865475f, 0); // rotArray[19]

			var flipperHingeJointPoint = new Vector3(0, 0, data.Height*0.5f * 0.001f); // half of flipper height
			RigidTransform m = new RigidTransform(surfaceWithFlipper.transform.rotation, flipperHingeJointPoint);
			Vector3 invFlipperHingeJointPoint = math.inverse(m).pos;

			var jointData = JointData.CreateLimitedHinge(
				new JointFrame(new RigidTransform(flipperRotationFix, flipperHingeJointPoint)),
				new JointFrame(new RigidTransform(surfaceWithFlipper.transform.rotation * flipperRotationFix, gameObject.transform.position + invFlipperHingeJointPoint)),
				new Math.FloatRange(d.AngleStart, d.AngleEnd));

			manager.AddComponentData(entity, new PhysicsJoint
			{
				JointData = jointData,
				EntityA = entity,
				EntityB = Entity.Null,
				EnableCollision = 1,
			});
		}

		public void AddMissingPhysicsComponents()
		{
			var d = GetMaterialData();
			// *** Warning: Magic numbers here! Replace it with somthing. See Mass, AngularDamping...

			// Add Physics Body Component
			var body = gameObject.AddComponent<PhysicsBodyAuthoring>();
			body.MotionType = BodyMotionType.Dynamic;
			body.Mass = 1;
			body.LinearDamping = 0.0f;                                  // will hinge joint will be attached to object, there will be on linear move
			body.AngularDamping = 0.1f;
			body.InitialLinearVelocity = float3.zero;
			body.InitialAngularVelocity = new float3(0,0,25); // float3.zero;
			body.GravityFactor = 1.0f;                                  // is it needed?
			body.OverrideDefaultMassDistribution = false;
			body.CustomTags = CustomPhysicsBodyTags.Nothing;            // Add flipper tag here?
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
				CurrentPhysicsTime = 0,
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


		/**
		 * Below are 24 rotations. All posible 90 deg.
		 * I used this to find correct flipper rotations.
		 * I left this here for future use.
		 * Data is based on: https://www.euclideanspace.com/maths/geometry/rotations/conversions/eulerToQuaternion/steps/index.htm
		 */
		static private float A = 0.7071067811865475f;
		static private float H = 0.5f;
		static public Quaternion[] all24rotations = {
			new Quaternion(0,0,0,1),
			new Quaternion(0,A,0,A),
			new Quaternion(0,1,0,0),
			new Quaternion(0,-A,0,A),

			new Quaternion(0,0,A,A),
			new Quaternion(H,H,H,H),
			new Quaternion(A,A,0,0),
			new Quaternion(-H,-H,H,H),

			new Quaternion(0,0,-A,A),
			new Quaternion(-H,H,-H,H),
			new Quaternion(-A,A,0,0),
			new Quaternion(H,-H,-H,H),

			new Quaternion(A,0,0,A),
			new Quaternion(H,H,-H,H),
			new Quaternion(0,A,-A,0),
			new Quaternion(H,-H,H,H),

			new Quaternion(1,0,0,0),
			new Quaternion(A,0,-A,0),
			new Quaternion(0,0,1,0),
			new Quaternion(A,0,A,0),

			new Quaternion(-A,0,0,A),
			new Quaternion(-H,H,H,H),
			new Quaternion(0,A,A,0),
			new Quaternion(-H,-H,-H,H),
		};

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
