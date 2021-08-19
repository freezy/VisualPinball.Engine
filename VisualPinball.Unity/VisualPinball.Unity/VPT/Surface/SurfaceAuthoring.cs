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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Surface")]
	public class SurfaceAuthoring : ItemMainRenderableAuthoring<Surface, SurfaceData>,
		IConvertGameObjectToEntity, ISurfaceAuthoring, IDragPointsAuthoring
	{
		#region Data

		[Tooltip("Top height of the wall, i.e. how high the wall goes.")]
		public float HeightTop = 50f;

		[Tooltip("Bottom height of the wall, i.e. at which height the wall starts.")]
		public float HeightBottom;

		public bool IsDroppable;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		public override ItemType ItemType => ItemType.Surface;

		protected override Surface InstantiateItem(SurfaceData data) => new Surface(data);
		protected override SurfaceData InstantiateData() => new SurfaceData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Surface, SurfaceData, SurfaceAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Surface, SurfaceData, SurfaceAuthoring>);

		public override IEnumerable<Type> ValidParents => SurfaceColliderAuthoring.ValidParentTypes
			.Concat(SurfaceSideMeshAuthoring.ValidParentTypes)
			.Concat(SurfaceTopMeshAuthoring.ValidParentTypes)
			.Distinct();

		public float Height(Vector2 _) => HeightTop + TableHeight;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// physics collision data
			var collComponent = GetComponentInChildren<SurfaceColliderAuthoring>();
			if (collComponent) {
				dstManager.AddComponentData(entity, new LineSlingshotData {
					IsDisabled = false,
					Threshold = collComponent.SlingshotThreshold,
				});
			}

			transform.GetComponentInParent<Player>().RegisterSurface(Item, entity, ParentEntity, gameObject);
		}

		public override IEnumerable<MonoBehaviour> SetData(SurfaceData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// main props
			HeightBottom = data.HeightBottom;
			HeightTop = data.HeightTop;
			IsDroppable = data.IsDroppable;
			DragPoints = data.DragPoints;

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderAuthoring>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;

				collComponent.HitEvent = data.HitEvent;
				collComponent.Threshold = data.Threshold;
				collComponent.IsBottomSolid = data.IsBottomSolid;

				collComponent.SlingshotForce = data.SlingshotForce;
				collComponent.SlingshotThreshold = data.SlingshotThreshold;

				collComponent.OverwritePhysics = data.OverwritePhysics;
				collComponent.Elasticity = data.Elasticity;
				collComponent.ElasticityFalloff = data.ElasticityFalloff;
				collComponent.Scatter = data.Scatter;
				collComponent.Friction = data.Friction;

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(SurfaceData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			// children mesh creation and visibility
			var topMesh = GetComponentInChildren<SurfaceTopMeshAuthoring>(true);
			if (topMesh) {
				topMesh.CreateMesh(data, textureProvider, materialProvider);
				topMesh.gameObject.SetActive(data.IsTopBottomVisible);
			}

			var sideMesh = GetComponentInChildren<SurfaceSideMeshAuthoring>(true);
			if (sideMesh) {
				sideMesh.CreateMesh(data, textureProvider, materialProvider);
				sideMesh.gameObject.SetActive(data.IsSideVisible);
			}

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderAuthoring>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override SurfaceData CopyDataTo(SurfaceData data, string[] materialNames, string[] textureNames)
		{
			// update the name
			data.Name = name;

			// main props
			data.IsDroppable = IsDroppable;
			data.HeightBottom = HeightBottom;
			data.HeightTop = HeightTop;
			data.DragPoints = DragPoints;

			// children visibility
			var topMesh = GetComponentInChildren<SurfaceTopMeshAuthoring>();
			data.IsTopBottomVisible = topMesh && topMesh.gameObject.activeInHierarchy;
			var sideMesh = GetComponentInChildren<SurfaceSideMeshAuthoring>();
			data.IsSideVisible = sideMesh && sideMesh.gameObject.activeInHierarchy;

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderAuthoring>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;

				data.HitEvent = collComponent.HitEvent;
				data.Threshold = collComponent.Threshold;
				data.IsBottomSolid = collComponent.IsBottomSolid;

				data.PhysicsMaterial = collComponent.PhysicsMaterial ? collComponent.PhysicsMaterial.name : string.Empty;
				data.SlingshotForce = collComponent.SlingshotForce;
				data.SlingshotThreshold = collComponent.SlingshotThreshold;

				data.OverwritePhysics = collComponent.OverwritePhysics;
				data.Elasticity = collComponent.Elasticity;
				data.ElasticityFalloff = collComponent.ElasticityFalloff;
				data.Scatter = collComponent.Scatter;
				data.Friction = collComponent.Friction;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		#region Editor Tooling

		private Vector3 DragPointCenter {
			get {
				var sum = Vertex3D.Zero;
				foreach (var t in DragPoints) {
					sum += t.Center;
				}
				var center = sum / DragPoints.Length;
				return new Vector3(center.X, center.Y, HeightTop);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => DragPoints.Length == 0 ? Vector3.zero : DragPointCenter;
		public override void SetEditorPosition(Vector3 pos) {
			if (DragPoints.Length == 0) {
				return;
			}
			var diff = (pos - DragPointCenter).ToVertex3D();
			diff.Z = 0f;
			foreach (var pt in DragPoints) {
				pt.Center += diff;
			}
			RebuildMeshes();
		}

		#endregion
	}
}
