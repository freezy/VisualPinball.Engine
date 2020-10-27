// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[RequiresEntityConversion]
	[AddComponentMenu("Visual Pinball/Game Item/Flipper")]
	public class FlipperAuthoring : ItemMainAuthoring<Flipper, FlipperData>,
		ISwitchAuthoring, ICoilAuthoring, IConvertGameObjectToEntity
	{
		protected override Flipper InstantiateItem(FlipperData data) => new Flipper(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Flipper, FlipperData, FlipperAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Flipper, FlipperData, FlipperAuthoring>);
		public override IEnumerable<Type> ValidParents => FlipperColliderAuthoring.ValidParentTypes
			.Concat(FlipperBaseMeshAuthoring.ValidParentTypes)
			.Concat(FlipperRubberMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		private static readonly Color EndAngleMeshColor = new Color32(0, 255, 248, 10);

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

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// update visibility
			Data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case FlipperBaseMeshAuthoring baseMeshAuthoring:
						Data.IsVisible = Data.IsVisible || baseMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case FlipperRubberMeshAuthoring rubberMeshAuthoring:
						Data.IsVisible = Data.IsVisible || rubberMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision: flipper is always collidable
		}

		public void OnRubberWidthUpdated(float before, float after)
		{
			if (before != 0 && after != 0f) {
				return;
			}

			if (before == 0) {
				ConvertedItem.CreateChild<FlipperRubberMeshAuthoring>(gameObject, FlipperMeshGenerator.Rubber);
			}

			if (after == 0) {
				var rubberAuthoring = GetComponentInChildren<FlipperRubberMeshAuthoring>();
				if (rubberAuthoring != null) {
					DestroyImmediate(rubberAuthoring.gameObject);
				}
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.StartAngle, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Data.StartAngle = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;

		public override Vector3 GetEditorScale() => new Vector3(Data.BaseRadius, Data.FlipperRadius, Data.Height);
		public override void SetEditorScale(Vector3 scale)
		{
			if (Data.BaseRadius > 0) {
				float endRadiusRatio = Data.EndRadius / Data.BaseRadius;
				Data.EndRadius = scale.x * endRadiusRatio;
			}
			Data.BaseRadius = scale.x;
			Data.FlipperRadius = scale.y;
			if (Data.Height > 0) {
				float rubberHeightRatio = Data.RubberHeight / Data.Height;
				Data.RubberHeight = scale.z * rubberHeightRatio;
				float rubberWidthRatio = Data.RubberWidth / Data.Height;
				Data.RubberWidth = scale.z * rubberWidthRatio;
			}
			Data.Height = scale.z;
		}

		protected void OnDrawGizmosSelected()
		{
			//base.OnDrawGizmosSelected();

			// draw end position mesh
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = EndAngleMeshColor;
			Gizmos.matrix = Matrix4x4.identity;
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -math.radians(Data.StartAngle))
			));
			foreach (var mf in mfs) {
				var t = mf.transform;
				var r = math.mul(baseRotation, quaternion.EulerXYZ(0, 0, math.radians(Data.EndAngle)));
				Gizmos.DrawWireMesh(mf.sharedMesh, t.position, r, t.lossyScale);
			}
		}

		private FlipperStaticData GetMaterialData()
		{
			float flipperRadius;
			if (Data.FlipperRadiusMin > 0 && Data.FlipperRadiusMax > Data.FlipperRadiusMin) {
				flipperRadius = Data.FlipperRadiusMax - (Data.FlipperRadiusMax - Data.FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				flipperRadius = math.max(flipperRadius, Data.BaseRadius - Data.EndRadius + 0.05f);

			} else {
				flipperRadius = Data.FlipperRadiusMax;
			}

			var endRadius = math.max(Data.EndRadius, 0.01f); // radius of flipper end
			flipperRadius = math.max(flipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			var angleStart = math.radians(Data.StartAngle);
			var angleEnd = math.radians(Data.EndAngle);

			if (angleEnd == angleStart) {
				// otherwise hangs forever in collisions/updates
				angleEnd += 0.0001f;
			}

			var tableData = Table.Data;

			// model inertia of flipper as that of rod of length flipper around its end
			var mass = Data.GetFlipperMass(tableData);
			var inertia = (float) (1.0 / 3.0) * mass * (flipperRadius * flipperRadius);

			return new FlipperStaticData {
				Inertia = inertia,
				AngleStart = angleStart,
				AngleEnd = angleEnd,
				Strength = Data.GetStrength(tableData),
				ReturnRatio = Data.GetReturnRatio(tableData),
				TorqueDamping = Data.GetTorqueDamping(tableData),
				TorqueDampingAngle = Data.GetTorqueDampingAngle(tableData),
				RampUpSpeed = Data.GetRampUpSpeed(tableData),

				EndRadius = endRadius,
				FlipperRadius = flipperRadius
			};
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
			var ratio = (math.max(Data.BaseRadius, 0.01f) - math.max(Data.EndRadius, 0.01f)) / math.max(Data.FlipperRadius, 0.01f);
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
