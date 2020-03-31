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
			var ltw = gameObject.transform.localToWorldMatrix;
			var zScale = (ltw * new Vector4(0, 0, 1, 0).normalized).magnitude;

			var surfaceWithFlipper = gameObject.transform.GetComponentInParent<TableBehavior>(); // Now it is table... but it may be surface with flipper too.

			//PhysicsJoint
			var flipperRotationFix = new Quaternion(0.7071067811865475f, 0, 0.7071067811865475f, 0); // rotArray[19]

			var flipperHingeJointPoint = new Vector3(0, 0, data.Height * 0.5f * zScale); // half of flipper height
			RigidTransform m = new RigidTransform(surfaceWithFlipper.transform.rotation, flipperHingeJointPoint);
			Vector3 invFlipperHingeJointPoint = math.inverse(m).pos;

			float angleStart = d.AngleStart % (float)(System.Math.PI * 2.0);
			float angleEnd = d.AngleEnd % (float)(System.Math.PI * 2.0);
			var jointData = JointData.CreateLimitedHinge(
				new JointFrame(new RigidTransform(flipperRotationFix, flipperHingeJointPoint)),
				new JointFrame(new RigidTransform(surfaceWithFlipper.transform.rotation * flipperRotationFix, gameObject.transform.position + invFlipperHingeJointPoint)),
				angleStart<angleEnd ? new Math.FloatRange(angleStart, angleEnd): new Math.FloatRange(angleEnd, angleStart));

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
			//ReplaceMeshWithCapsuleShapes();
			ReplaceMeshWithCylindersShapes();
			var d = GetMaterialData();

			// *** Warning: Magic numbers here! Replace it with somthing. See Mass, AngularDamping...

			// Add Physics Body Component
			var body = gameObject.AddComponent<PhysicsBodyAuthoring>();
			body.MotionType = BodyMotionType.Dynamic;
			body.Mass = 10;
			body.LinearDamping = 0.0f;                                  // will hinge joint will be attached to object, there will be on linear move
			body.AngularDamping = 0.1f;
			body.InitialLinearVelocity = float3.zero;
			body.InitialAngularVelocity = float3.zero;
			body.GravityFactor = 1.0f;                                  // is it needed?
			body.OverrideDefaultMassDistribution = false;
			body.CustomTags = CustomPhysicsBodyTags.Nothing;            // Add flipper tag here?	

			SetCollisionsFilters(gameObject);
		}

		private void ReplaceMeshWithCapsuleShapes()
		{
			var d = GetMaterialData();
			var ltw = gameObject.transform.localToWorldMatrix;
			var scale = (ltw * new Vector4(1, 1, 1, 0).normalized).magnitude;
			var halfHeight = data.Height * 0.5f;                // This should be ball radius!

			GameObject.Destroy(gameObject.GetComponent<PhysicsShapeAuthoring>());

			// base capsule
			var goBase = new GameObject();
			goBase.transform.parent = gameObject.transform;
			var baseCap = goBase.AddComponent<PhysicsShapeAuthoring>();
			baseCap.BelongsTo = PhysicsCategoryTags.Nothing;
			baseCap.SetCapsule(new CapsuleGeometryAuthoring
			{
				Orientation = ltw.rotation,
				Center = ltw.MultiplyPoint(new Vector3(0, 0, halfHeight)),
				Height = data.Height * scale,
				Radius = data.BaseRadius * scale
			});

			// front capsule math
			Vector3 frontNormal = new Vector3(-data.FlipperRadius, data.BaseRadius - data.EndRadius, 0).normalized;
			Vector3 beg = new Vector3(0, 0, halfHeight) + frontNormal * (data.BaseRadius - data.EndRadius);
			if (data.StartAngle < 0 | data.StartAngle > 180)
				beg.x = -beg.x;
			Vector3 end = new Vector3(0, -data.FlipperRadius, halfHeight);
			Vector3 cen = (beg + end) * 0.5f;

			float len = (beg - end).magnitude + data.EndRadius * 2;
			Quaternion rot = Quaternion.LookRotation(beg - end);

			// front capsule
			var goFront = new GameObject();
			goFront.transform.parent = gameObject.transform;
			var frontCap = goFront.AddComponent<PhysicsShapeAuthoring>();
			frontCap.SetCapsule(new CapsuleGeometryAuthoring
			{
				Orientation = ltw.rotation * rot,
				Center = ltw.MultiplyPoint(cen),
				Height = len * scale,
				Radius = data.EndRadius * scale
			});
		}

		private void ReplaceMeshWithCylindersShapes()
		{
			var d = GetMaterialData();
			var ltw = gameObject.transform.localToWorldMatrix;
			var zScale = (ltw * new Vector4(0, 0, 1, 0).normalized).magnitude;
			var xyScale = (ltw * new Vector4(1, 1, 0, 0).normalized).magnitude;
			var halfHeight = data.Height * 0.5f;                // This should be ball radius!

			GameObject.Destroy(gameObject.GetComponent<PhysicsShapeAuthoring>());

			// base cylinder
			var goBase = new GameObject();
			goBase.transform.parent = gameObject.transform;
			var beg = goBase.AddComponent<PhysicsShapeAuthoring>();
			beg.SetCylinder(new CylinderGeometry
			{
				Orientation = ltw.rotation,
				Center = ltw.MultiplyPoint(new Vector3(0, 0, halfHeight)),
				BevelRadius = 0,
				Height = data.Height * zScale,
				Radius = data.BaseRadius * xyScale
			});

			// end cylinder
			var goEnd = new GameObject();
			goEnd.transform.parent = gameObject.transform;
			var end = goEnd.AddComponent<PhysicsShapeAuthoring>();
			end.SetCylinder(new CylinderGeometry
			{
				Orientation = ltw.rotation,
				Center = ltw.MultiplyPoint(new Vector3(0, -data.FlipperRadius, halfHeight)),
				BevelRadius = 0,
				Height = data.Height * zScale,
				Radius = data.EndRadius * xyScale
			});

			// flat mesh
			var goFlat = new GameObject();
			goFlat.transform.parent = gameObject.transform;
			PhysicsShapeAuthoring flat = goFlat.AddComponent<PhysicsShapeAuthoring>();
			var flatMesh = new Mesh();
			Vector3 n = new Vector3(data.FlipperRadius, data.BaseRadius - data.EndRadius, 0).normalized;
			Vector3 m = new Vector3(-data.FlipperRadius, data.BaseRadius - data.EndRadius, 0).normalized;
			float r1 = data.BaseRadius;
			float r2 = data.EndRadius;
			float h = data.Height;
			float l = data.FlipperRadius;

			Vector3[] verts = {
				new Vector3(0,0,h) + n*r1,
				new Vector3(0,-l,h) + n*r2,
				new Vector3(0,0,0) + n*r1,
				new Vector3(0,-l,0) + n*r2,

				new Vector3(0,0,h) + m*r1,
				new Vector3(0,-l,h) + m*r2,
				new Vector3(0,0,0) + m*r1,
				new Vector3(0,-l,0) + m*r2,
			};

			for (int i = 0; i < 8; ++i)
			{
				verts[i] = ltw.MultiplyPoint(verts[i]);
			}

			int[] tri = { 0, 1, 2,    1, 3, 2,    5, 4, 6,    5, 6, 7 };

			// we don't need normals?!
			//Vector3[] norm = { m,m,m,m,  n,n,n,n };
			//flatMesh.normals = norm;

			flatMesh.vertices = verts;
			flatMesh.triangles = tri;
			flat.SetMesh(flatMesh);
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

		protected override uint PhysicsTag { get; } = PhysicsTags.Flipper;

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
