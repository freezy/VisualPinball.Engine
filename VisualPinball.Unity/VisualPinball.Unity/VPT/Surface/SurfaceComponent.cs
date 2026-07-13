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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[SelectionBase]
	[PackAs("Surface")]
	[AddComponentMenu("Pinball/Game Item/Surface")]
	public class SurfaceComponent : MainRenderableComponent<SurfaceData>, ISurfaceComponent, IPackable,
		IDragPointSplineOwner
	{
		#region Data

		[Tooltip("Top height of the wall, i.e. how high the wall goes.")]
		public float HeightTop = 50f;

		[Tooltip("Bottom height of the wall, i.e. at which height the wall starts.")]
		public float HeightBottom;

		[SerializeField, HideInInspector]
		private DragPointData[] _dragPoints;

		[SerializeField]
		private DragPointSplineComponent _dragPointSpline;

		public DragPointData[] DragPoints {
			get => GetOrCreateDragPointSpline().DragPoints;
			set {
				if (!_dragPointSpline) {
					_dragPoints = value;
					GetOrCreateDragPointSpline();
				} else {
					_dragPointSpline.SetDragPoints(value);
				}
			}
		}
		public DragPointSplineComponent DragPointSpline => GetOrCreateDragPointSpline();

		[SerializeField]
		public Vector3 uvOffset = Vector3.zero;

		#endregion

		#region Packaging

		public byte[] Pack() => SurfacePackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => Array.Empty<byte>();

		public void Unpack(byte[] bytes) => SurfacePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

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

		public SurfaceApi SurfaceApi { get; private set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			SurfaceApi = new SurfaceApi(gameObject, Player, physicsEngine);

			Player.Register(SurfaceApi, this);
			if (GetComponentInChildren<SurfaceColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		#endregion

		#region Transformation


		public float Height(Vector2 _) => HeightTop;

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			SetChildrenZPosition(_ => HeightTop);
		}

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

			CenterPivot();

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
			if (surfaceComponent) {
				HeightTop = surfaceComponent.HeightTop;
				HeightBottom = surfaceComponent.HeightBottom;
				DragPoints = surfaceComponent.DragPoints.Select(dp => dp.Clone()).ToArray();

			}
			RebuildMeshes();
		}

		private void CenterPivot()
		{
			// TODO move origin to the top.
			// in order to do that, we'll need to treat top and bottom height differently:
			// - top height is at local z = 0
			// - changing top height will both transform on local z and change the height of the object
			// - changing bottom height will just change the height
			// - change mesh and collider creation to create top-down instead of bottom-up.

			var dragPoints = DragPoints;
			var centerVpx = dragPoints.Aggregate(Vector3.zero, (current, dragPoint) => current + dragPoint.Center.ToUnityVector3());
			centerVpx /= dragPoints.Length;

			if (uvOffset == Vector3.zero) {
				uvOffset = centerVpx;
			}

			transform.Translate(centerVpx.TranslateToWorld(transform) - transform.position);
			foreach (var dragPoint in dragPoints) {
				dragPoint.Center -= centerVpx.ToVertex3D();
			}
			DragPoints = dragPoints;
			RebuildMeshes();
		}

		private DragPointSplineComponent GetOrCreateDragPointSpline()
		{
			_dragPointSpline = DragPointSplineComponent.GetOrCreate(this,
				_dragPointSpline, _dragPoints);
			_dragPoints = null;
			return _dragPointSpline;
		}

		MonoBehaviour IDragPointSplineOwner.SplineOwner => this;
		Transform IDragPointSplineOwner.SplineTransform => transform;
		DragPointSplineComponent IDragPointSplineOwner.SplineComponent => DragPointSpline;
		bool IDragPointSplineOwner.SplineClosed => true;
		bool IDragPointSplineOwner.SplinePlanar => true;
		void IDragPointSplineOwner.ApplySplineConstraints(Spline spline, int knotIndex,
			SplineModification modification, IReadOnlyList<float3> previousPositions) { }
		void IDragPointSplineOwner.RebuildSplineMeshes() => RebuildMeshes();

		#endregion

		#region State

		internal SurfaceState CreateState()
		{
			// physics collision data
			var collComponent = GetComponentInChildren<SurfaceColliderComponent>();
			if (!collComponent) {
				return default;
			}

			return new SurfaceState(new LineSlingshotState {
				IsDisabled = false,
				Threshold = collComponent.SlingshotThreshold,
			});
		}

		#endregion
	}
}
