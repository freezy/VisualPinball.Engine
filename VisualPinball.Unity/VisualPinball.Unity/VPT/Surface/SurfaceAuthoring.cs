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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Surface")]
	public class SurfaceAuthoring : ItemMainRenderableAuthoring<Surface, SurfaceData>,
		IConvertGameObjectToEntity, IDragPointsEditable
	{
		protected override Surface InstantiateItem(SurfaceData data) => new Surface(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Surface, SurfaceData, SurfaceAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Surface, SurfaceData, SurfaceAuthoring>);

		public override IEnumerable<Type> ValidParents => SurfaceColliderAuthoring.ValidParentTypes
			.Concat(SurfaceSideMeshAuthoring.ValidParentTypes)
			.Concat(SurfaceTopMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			dstManager.AddComponentData(entity, new LineSlingshotData {
				IsDisabled = false,
				Threshold = Data.SlingshotThreshold,
			});
			transform.GetComponentInParent<Player>().RegisterSurface(Item, entity, ParentEntity, gameObject);
		}

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// update visibility
			Data.IsSideVisible = false;
			Data.IsTopBottomVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case SurfaceSideMeshAuthoring meshAuthoring:
						Data.IsSideVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
					case SurfaceTopMeshAuthoring meshAuthoring:
						Data.IsTopBottomVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			Data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is SurfaceColliderAuthoring colliderAuthoring) {
					Data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}
		}

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Surface>(Name);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() {
			if (Data == null || Data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return Data.DragPoints[0].Center.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos) {
			if (Data == null || Data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D() - Data.DragPoints[0].Center;
			diff.Z = 0f;
			Data.DragPoints[0].Center = pos.ToVertex3D();
			for (var i = 1; i < Data.DragPoints.Length; i++) {
				var pt = Data.DragPoints[i];
				pt.Center += diff;
			}
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => Data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { Data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, Data.HeightBottom);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth , DragPointExposure.SlingShot , DragPointExposure.Texture };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
