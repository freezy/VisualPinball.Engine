#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Flipper;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[RequiresEntityConversion]
	[AddComponentMenu("Visual Pinball/Flipper")]
	public class FlipperAuthoring : ItemAuthoring<Flipper, FlipperData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{ FlipperMeshGenerator.BaseName, FlipperMeshGenerator.RubberName };

		protected override Flipper GetItem() => new Flipper(data);

		private static readonly Color EndAngleMeshColor = new Color32(0, 255, 248, 10);
		private static readonly Color AabbColor = new Color32(255, 0, 252, 255);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Flipper>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var d = GetMaterialData();
			dstManager.AddComponentData(entity, d);
			dstManager.AddComponentData(entity, GetMovementData(d));
			dstManager.AddComponentData(entity, GetVelocityData(d));
			dstManager.AddComponentData(entity, GetHitData());
			dstManager.AddComponentData(entity, new SolenoidStateData { Value = false });

			// register
			transform.GetComponentInParent<Player>().RegisterFlipper(Item, entity, gameObject);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.StartAngle, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.StartAngle = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => new Vector3(data.BaseRadius, data.FlipperRadius, data.Height);
		public override void SetEditorScale(Vector3 scale)
		{
			if (data.BaseRadius > 0) {
				float endRadiusRatio = data.EndRadius / data.BaseRadius;
				data.EndRadius = scale.x * endRadiusRatio;
			}
			data.BaseRadius = scale.x;
			data.FlipperRadius = scale.y;
			if (data.Height > 0) {
				float rubberHeightRatio = data.RubberHeight / data.Height;
				data.RubberHeight = scale.z * rubberHeightRatio;
				float rubberWidthRatio = data.RubberWidth / data.Height;
				data.RubberWidth = scale.z * rubberWidthRatio;
			}
			data.Height = scale.z;
		}

		private void OnDrawGizmosSelected()
		{
			var ltw = transform.GetComponentInParent<TableAuthoring>().gameObject.transform.localToWorldMatrix;

			// draw end position mesh
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = EndAngleMeshColor;
			Gizmos.matrix = Matrix4x4.identity;
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -math.radians(data.StartAngle))
			));
			foreach (var mf in mfs) {
				var t = mf.transform;
				var r = math.mul(baseRotation, quaternion.EulerXYZ(0, 0, data.EndAngle));
				Gizmos.DrawWireMesh(mf.sharedMesh, t.position, r, t.lossyScale);
			}

			Item.Init(Table);
			var hitObj = Item.GetHitShapes()[0];
			hitObj.CalcHitBBox();
			var aabb = hitObj.HitBBox;

			const float s = 0.1f;
			var center = new Vector3(data.Center.X, data.Center.Y, 0f);
			Gizmos.color = AabbColor;
			DrawAabb(ltw, aabb);
			//Gizmos.DrawCube(ltw.MultiplyPoint(center), new Vector3(s, s, s));
			//Gizmos.DrawCube(ltw * aabb.Center.ToUnityVector3(), new Vector3(s, s, s));
		}

		private FlipperStaticData GetMaterialData()
		{
			float flipperRadius;
			if (data.FlipperRadiusMin > 0 && data.FlipperRadiusMax > data.FlipperRadiusMin) {
				flipperRadius = data.FlipperRadiusMax - (data.FlipperRadiusMax - data.FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				flipperRadius = math.max(flipperRadius, data.BaseRadius - data.EndRadius + 0.05f);

			} else {
				flipperRadius = data.FlipperRadiusMax;
			}

			var endRadius = math.max(data.EndRadius, 0.01f); // radius of flipper end
			flipperRadius = math.max(flipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			var angleStart = math.radians(data.StartAngle);
			var angleEnd = math.radians(data.EndAngle);

			if (angleEnd == angleStart) {
				// otherwise hangs forever in collisions/updates
				angleEnd += 0.0001f;
			}

			var tableData = Table.Data;

			// model inertia of flipper as that of rod of length flipper around its end
			var mass = data.GetFlipperMass(tableData);
			var inertia = (float) (1.0 / 3.0) * mass * (flipperRadius * flipperRadius);

			return new FlipperStaticData {
				Inertia = inertia,
				AngleStart = angleStart,
				AngleEnd = angleEnd,
				Strength = data.GetStrength(tableData),
				ReturnRatio = data.GetReturnRatio(tableData),
				TorqueDamping = data.GetTorqueDamping(tableData),
				TorqueDampingAngle = data.GetTorqueDampingAngle(tableData),
				RampUpSpeed = data.GetRampUpSpeed(tableData),

				EndRadius = endRadius,
				FlipperRadius = flipperRadius
			};
		}

		protected void DrawAabb(Matrix4x4 ltw, Rect3D aabb)
		{
			var p00 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZHigh));
			var p01 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZHigh));
			var p02 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZHigh));
			var p03 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZHigh));

			var p10 = ltw.MultiplyPoint(new Vector3( aabb.Left, aabb.Top, aabb.ZLow));
			var p11 = ltw.MultiplyPoint(new Vector3(aabb.Left, aabb.Bottom, aabb.ZLow));
			var p12 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Bottom, aabb.ZLow));
			var p13 = ltw.MultiplyPoint(new Vector3(aabb.Right, aabb.Top, aabb.ZLow));

			Gizmos.color = AabbColor;
			Gizmos.DrawLine(p00, p01);
			Gizmos.DrawLine(p01, p02);
			Gizmos.DrawLine(p02, p03);
			Gizmos.DrawLine(p03, p00);

			Gizmos.DrawLine(p10, p11);
			Gizmos.DrawLine(p11, p12);
			Gizmos.DrawLine(p12, p13);
			Gizmos.DrawLine(p13, p10);

			Gizmos.DrawLine(p00, p10);
			Gizmos.DrawLine(p01, p11);
			Gizmos.DrawLine(p02, p12);
			Gizmos.DrawLine(p03, p13);
		}

		private FlipperMovementData GetMovementData(FlipperStaticData d)
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

		private static FlipperVelocityData GetVelocityData(FlipperStaticData d)
		{
			return new FlipperVelocityData {
				AngularAcceleration = 0f,
				ContactTorque = 0f,
				CurrentTorque = 0f,
				Direction = d.AngleEnd >= d.AngleStart,
				IsInContact = false
			};
		}

		private FlipperHitData GetHitData()
		{
			var ratio = (math.max(data.BaseRadius, 0.01f) - math.max(data.EndRadius, 0.01f)) / math.max(data.FlipperRadius, 0.01f);
			var zeroAngNorm = new float2(
				math.sqrt(1.0f - ratio * ratio), // F2 Norm, used in Green's transform, in FPM time search  // =  sinf(faceNormOffset)
				-ratio                              // F1 norm, change sign of x component, i.e -zeroAngNorm.x // = -cosf(faceNormOffset)
			);

			return new FlipperHitData {
				ZeroAngNorm = zeroAngNorm,
				HitMomentBit = true,
				HitVelocity = new float2(),
				LastHitFace = false,
			};
		}
	}
}
