// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Ramp")]
	public class RampAuthoring : ItemMainRenderableAuthoring<Ramp, RampData>, IDragPointsEditable, IConvertGameObjectToEntity
	{
		#region Data

		public float DepthBias = 0f;

		public DragPointData[] DragPoints;

		public float Elasticity;

		public float Friction;

		public bool HitEvent = false;

		public float HeightBottom = 0f;

		public float HeightTop = 50f;

		public int ImageAlignment = RampImageAlignment.ImageModeWorld;

		public bool ImageWalls = true;

		public bool IsCollidable = true;

		public float LeftWallHeight = 62f;

		public float LeftWallHeightVisible = 30f;

		public bool OverwritePhysics = true;

		public int Type = RampType.RampTypeFlat;

		public float RightWallHeight = 62f;

		public float RightWallHeightVisible = 30f;

		public float Scatter;

		public string PhysicsMaterial = string.Empty;

		public float Threshold;

		public float WidthBottom = 75f;

		public float WidthTop = 60f;

		public float WireDiameter = 8f;

		public float WireDistanceX = 38f;

		public float WireDistanceY = 88f;

		public bool Points;

		#endregion

		protected override Ramp InstantiateItem(RampData data) => new Ramp(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Ramp, RampData, RampAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Ramp, RampData, RampAuthoring>);

		public override IEnumerable<Type> ValidParents => RampColliderAuthoring.ValidParentTypes
			.Concat(RampFloorMeshAuthoring.ValidParentTypes)
			.Concat(RampWallMeshAuthoring.ValidParentTypes)
			.Concat(RampWireMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRamp(Item, entity, ParentEntity, gameObject);
		}

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// update visibility
			Data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case RampFloorMeshAuthoring meshAuthoring:
						Data.IsVisible = Data.IsVisible || meshAuthoring.gameObject.activeInHierarchy;
						break;
					case RampWallMeshAuthoring meshAuthoring:
						Data.IsVisible = Data.IsVisible || meshAuthoring.gameObject.activeInHierarchy;
						break;
					case RampWireMeshAuthoring meshAuthoring:
						Data.IsVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			Data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is RampColliderAuthoring colliderAuthoring) {
					Data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}
		}

		public void UpdateMeshComponents(int rampTypeBefore, int rampTypeAfter)
		{
			var rampFlatBefore = rampTypeBefore == RampType.RampTypeFlat;
			var rampFlatAfter = rampTypeAfter == RampType.RampTypeFlat;
			if (rampFlatBefore == rampFlatAfter) {
				return;
			}

			var convertedItem = new ConvertedItem<Ramp, RampData, RampAuthoring>(gameObject);
			if (rampFlatAfter) {
				convertedItem.Destroy<RampWireMeshAuthoring>();
				convertedItem.AddMeshAuthoring<RampFloorMeshAuthoring>(RampMeshGenerator.Floor, false);
				convertedItem.AddMeshAuthoring<RampWallMeshAuthoring>(RampMeshGenerator.Wall, false);

			} else {
				convertedItem.Destroy<RampFloorMeshAuthoring>();
				convertedItem.Destroy<RampWallMeshAuthoring>();
				convertedItem.AddMeshAuthoring<RampWireMeshAuthoring>(RampMeshGenerator.Wires, false);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => DragPoints.Length == 0 ? Vector3.zero : DragPoints[0].Center.ToUnityVector3();

		public override void SetEditorPosition(Vector3 pos)
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D() - Data.DragPoints[0].Center;
			diff.Z = 0f;
			Data.DragPoints[0].Center = pos.ToVertex3D();
			for (int i = 1; i < Data.DragPoints.Length; i++) {
				var pt = Data.DragPoints[i];
				pt.Center += diff;
			}
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }

		public DragPointData[] GetDragPoints() => Data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { Data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, Data.HeightBottom);
		public Vector3 GetDragPointOffset(float ratio) => new Vector3(0.0f, 0.0f, (Data.HeightTop - Data.HeightBottom) * ratio);
		public bool PointsAreLooping() => false;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new DragPointExposure[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.ThreeD;
	}
}
