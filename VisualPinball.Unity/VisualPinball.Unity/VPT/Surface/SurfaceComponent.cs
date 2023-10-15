// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Surface")]
	public class SurfaceComponent : MainRenderableComponent<SurfaceData>, ISurfaceComponent
	{
		#region Data

		[Tooltip("Top height of the wall, i.e. how high the wall goes.")]
		public float HeightTop = 50f;

		[Tooltip("Bottom height of the wall, i.e. at which height the wall starts.")]
		public float HeightBottom;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Surface;
		public override string ItemName => "Surface";

		public override SurfaceData InstantiateData() => new SurfaceData();

		public override bool HasProceduralMesh => true;


		protected override Type MeshComponentType { get; } = typeof(MeshComponent<SurfaceData, SurfaceComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<SurfaceData, SurfaceComponent>);

		#endregion

		#region Runtime

		private void Awake()
		{
			// register at player
			GetComponentInParent<Player>().RegisterSurface(this);
			if (GetComponentInChildren<SurfaceColliderComponent>()) {
				GetComponentInParent<PhysicsEngine>().Register(this);
			}
		}

		#endregion

		#region Transformation

		public float Height(Vector2 _) => HeightTop + PlayfieldHeight;

		public override void OnPlayfieldHeightUpdated() => RebuildMeshes();

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(SurfaceData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// main props
			HeightBottom = data.HeightBottom;
			HeightTop = data.HeightTop;
			DragPoints = data.DragPoints;

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
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

		public override IEnumerable<MonoBehaviour> SetReferencedData(SurfaceData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// children mesh creation and visibility
			var topMesh = GetComponentInChildren<SurfaceTopMeshComponent>(true);
			if (topMesh) {
				topMesh.CreateMesh(data, table, textureProvider, materialProvider);
				topMesh.gameObject.SetActive(data.IsTopBottomVisible);
			}

			var sideMesh = GetComponentInChildren<SurfaceSideMeshComponent>(true);
			if (sideMesh) {
				sideMesh.CreateMesh(data, table, textureProvider, materialProvider);
				sideMesh.gameObject.SetActive(data.IsSideVisible);
			}

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override SurfaceData CopyDataTo(SurfaceData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// update the name
			data.Name = name;

			// main props
			data.HeightBottom = HeightBottom;
			data.HeightTop = HeightTop;
			data.DragPoints = DragPoints;

			// children visibility
			var topMesh = GetComponentInChildren<SurfaceTopMeshComponent>();
			data.IsTopBottomVisible = topMesh && topMesh.gameObject.activeInHierarchy;
			var sideMesh = GetComponentInChildren<SurfaceSideMeshComponent>();
			data.IsSideVisible = sideMesh && sideMesh.gameObject.activeInHierarchy;

			// collider data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
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

		public override void CopyFromObject(GameObject go)
		{
			var surfaceComponent = go.GetComponent<SurfaceComponent>();
			if (surfaceComponent != null) {
				HeightTop = surfaceComponent.HeightTop;
				HeightBottom = surfaceComponent.HeightBottom;
				_dragPoints = surfaceComponent._dragPoints.Select(dp => dp.Clone()).ToArray();

			} else {
				MoveDragPointsTo(_dragPoints, go.transform.localPosition.TranslateToVpx());
			}

			UpdateTransforms();
			RebuildMeshes();
		}

		#endregion

		#region State

		internal SurfaceState CreateState()
		{
			// physics collision data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
			if (!collComponent) {
				return default;
			}

			return new SurfaceState(gameObject.GetInstanceID(), new LineSlingshotState {
				IsDisabled = false,
				Threshold = collComponent.SlingshotThreshold,
			});
		}

		#endregion

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
