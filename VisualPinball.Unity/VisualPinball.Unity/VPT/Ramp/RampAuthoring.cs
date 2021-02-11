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
			transform.GetComponentInParent<Player>().RegisterRamp(Item, entity, gameObject);
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

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Ramp>(Name);
			}
		}

		public void UpdateMeshComponents(int rampTypeBefore, int rampTypeAfter)
		{
			var rampFlatBefore = rampTypeBefore == RampType.RampTypeFlat;
			var rampFlatAfter = rampTypeAfter == RampType.RampTypeFlat;
			if (rampFlatBefore == rampFlatAfter) {
				return;
			}

			if (rampFlatAfter) {
				var flatRampAuthoring = GetComponentInChildren<RampWireMeshAuthoring>();
				if (flatRampAuthoring != null) {
					DestroyImmediate(flatRampAuthoring.gameObject);
				}
				ConvertedItem.CreateChild<RampFloorMeshAuthoring>(gameObject, RampMeshGenerator.Floor);
				ConvertedItem.CreateChild<RampWallMeshAuthoring>(gameObject, RampMeshGenerator.Wall);

			} else {
				var flatFloorAuthoring = GetComponentInChildren<RampFloorMeshAuthoring>();
				if (flatFloorAuthoring != null) {
					DestroyImmediate(flatFloorAuthoring.gameObject);
				}
				var flatWallAuthoring = GetComponentInChildren<RampWallMeshAuthoring>();
				if (flatWallAuthoring != null) {
					DestroyImmediate(flatWallAuthoring.gameObject);
				}
				ConvertedItem.CreateChild<RampWireMeshAuthoring>(gameObject, RampMeshGenerator.Wires);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition()
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return Data.DragPoints[0].Center.ToUnityVector3();
		}
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
