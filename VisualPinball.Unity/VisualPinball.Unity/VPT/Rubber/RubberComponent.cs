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
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[SelectionBase]
	[PackAs("Rubber")]
	[AddComponentMenu("Pinball/Game Item/Rubber")]
	public class RubberComponent : MainRenderableComponent<RubberData>, IRubberData, IPackable,
		IDragPointSplineOwner
	{
		#region Data

		[Tooltip("Height of the rubber (z-axis).")]
		public float Height {
			get => transform.localPosition.TranslateToVpx().z;
			set => transform.localPosition = new Vector3(transform.localPosition.x, Physics.ScaleToWorld(value), transform.localPosition.z);
		}

		[Min(0)]
		[Tooltip("How thick the rubber band is rendered.")]
		public int _thickness = 8;

		[SerializeField, HideInInspector]
		private DragPointData[] _dragPoints;

		[SerializeField]
		private DragPointSplineComponent _dragPointSpline;

		[SerializeField] private RubberPathSource _pathSource = RubberPathSource.Spline;
		[SerializeField] private RubberGuideBinding[] _guideBindings = Array.Empty<RubberGuideBinding>();
		[SerializeField, HideInInspector] private RubberPathElement[] _bakedPath = Array.Empty<RubberPathElement>();
		[SerializeField, HideInInspector] private uint _bakeVersion;
		[SerializeField, HideInInspector] private Hash128 _bakeInputHash;
		[SerializeField, HideInInspector] private Matrix4x4 _bakeFrameToLocal = Matrix4x4.identity;
		[SerializeField, Min(0f)] private float _restLength;

		[NonSerialized]
		private Vertex3D[] _scalingDragPoints;

		#endregion

		#region IRubberData

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
		public int Thickness => math.max(1, _thickness); // don't allow zero thickness
		public RubberPathSource PathSource => _pathSource;
		public IReadOnlyList<RubberGuideBinding> GuideBindings => _guideBindings ?? Array.Empty<RubberGuideBinding>();
		public IReadOnlyList<RubberPathElement> BakedPath => _bakedPath ?? Array.Empty<RubberPathElement>();
		public uint BakeVersion => _bakeVersion;
		public Hash128 BakeInputHash => _bakeInputHash;
		public Matrix4x4 BakeFrameToLocal => _bakeFrameToLocal;
		public float RestLength {
			get => _restLength;
			set => _restLength = math.max(0f, value);
		}

		public bool HasValidGuidedPath {
			get => RubberAutofit.GetStatus(this).IsValid;
		}

		#endregion

		#region Packaging

		public byte[] Pack() => RubberPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => RubberPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberReferencesPackable.Unpack(data, this, refs);

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Rubber;
		public override string ItemName => "Rubber";

		public override RubberData InstantiateData() => new RubberData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<RubberData, RubberComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<RubberData, RubberComponent>);

		#endregion

		#region Runtime

		public RubberApi RubberApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			RubberApi = new RubberApi(gameObject, player, physicsEngine);

			player.Register(RubberApi, this);
			RegisterPhysics(physicsEngine);
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(RubberData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };
			ResetToSplineSource();

			// reset origin to bounding box center and move points accordingly
			var min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
			var max = new float3(float.MinValue, float.MinValue, float.MinValue);
			foreach (var dp in data.DragPoints) {
				min = math.min(min, dp.Center.ToUnityFloat3());
				max = math.max(max, dp.Center.ToUnityFloat3());
			}
			var boundingBoxCenter = (max + min) / 2f;
			boundingBoxCenter.z = data.Height;
			foreach (var dp in data.DragPoints) {
				dp.Center = new Vertex3D(
					dp.Center.X - boundingBoxCenter.x,
					dp.Center.Y - boundingBoxCenter.y,
					0
				);
			}

			// add rotation to the final matrix..
			var rotMatrix = float4x4.EulerZYX(math.radians(new float3(data.RotX, data.RotY, data.RotZ)));
			transform.SetFromMatrix(math.mul(float4x4.Translate(boundingBoxCenter), rotMatrix).TransformVpxInWorld());

			// geometry
			_thickness = data.Thickness;
			DragPoints = data.DragPoints;

			// collider data
			var collComponent = GetComponent<RubberColliderComponent>();
			if (collComponent) {
				collComponent.Mode = RubberColliderMode.Legacy;
				collComponent.RubberPhysicsMaterial = null;
				collComponent.enabled = data.IsCollidable;

				collComponent.HitEvent = data.HitEvent;
				collComponent.ZOffset = data.Height - data.HitHeight; // check if not -1
				collComponent.OverwritePhysics = data.OverwritePhysics;
				collComponent.Elasticity = data.Elasticity;
				collComponent.ElasticityFalloff = data.ElasticityFalloff;
				collComponent.Friction = data.Friction;
				collComponent.Scatter = data.Scatter;

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(RubberData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// mesh
			var mesh = GetComponent<RubberMeshComponent>();
			if (mesh) {
				mesh.CreateMesh(data, table, textureProvider, materialProvider);
				mesh.enabled = data.IsVisible;
				SetEnabled<Renderer>(data.IsVisible);
			}

			// collider data
			var collComponent = GetComponentInChildren<RubberColliderComponent>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override RubberData CopyDataTo(RubberData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// update the name
			data.Name = name;

			// geometry
			data.Thickness = _thickness;
			data.DragPoints = DragPoints;

			// visibility
			data.IsVisible = GetEnabled<Renderer>();

			// collision
			var collComponent = GetComponentInChildren<RubberColliderComponent>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;

				data.HitEvent = collComponent.HitEvent;
				data.HitHeight = data.Height - collComponent.ZOffset; // check if not -1

				data.PhysicsMaterial = collComponent.PhysicsMaterial ? collComponent.PhysicsMaterial.name : string.Empty;
				data.OverwritePhysics = collComponent.OverwritePhysics;
				data.Elasticity = collComponent.Elasticity;
				data.ElasticityFalloff = collComponent.ElasticityFalloff;
				data.Friction = collComponent.Friction;
				data.Scatter = collComponent.Scatter;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var srcMainComp = go.GetComponent<RubberComponent>();
			if (srcMainComp) {
				_thickness = srcMainComp._thickness;
				DragPoints = srcMainComp.DragPoints.Select(dp => dp.Clone()).ToArray();
				_pathSource = srcMainComp._pathSource;
				_guideBindings = srcMainComp.GuideBindings.ToArray();
				_bakedPath = srcMainComp.BakedPath.ToArray();
				_bakeVersion = srcMainComp._bakeVersion;
				_bakeInputHash = srcMainComp._bakeInputHash;
				_bakeFrameToLocal = srcMainComp._bakeFrameToLocal;
				_restLength = srcMainComp._restLength;
			}
			RebuildMeshes();
		}

		public void SetGuideBindings(IEnumerable<RubberGuideBinding> bindings)
		{
			_guideBindings = bindings?.ToArray() ?? Array.Empty<RubberGuideBinding>();
			_pathSource = RubberPathSource.Guides;
			_bakeVersion = 0;
			_bakeInputHash = default;
		}

		public void ApplyGuidedBake(RubberPathElement[] path, DragPointData[] sampledDragPoints,
			Matrix4x4 bakeFrameToLocal, Hash128 inputHash, uint bakeVersion)
		{
			if (_pathSource != RubberPathSource.Guides) {
				throw new InvalidOperationException("A guided bake requires guide bindings to be authoritative.");
			}
			_bakedPath = path?.ToArray() ?? Array.Empty<RubberPathElement>();
			_bakeFrameToLocal = bakeFrameToLocal;
			_bakeInputHash = inputHash;
			_bakeVersion = bakeVersion;
			if (sampledDragPoints != null) {
				DragPoints = sampledDragPoints;
			}
		}

		public void DetachFromGuides()
		{
			ResetToSplineSource();
			var collider = GetComponent<RubberColliderComponent>();
			if (collider) {
				collider.Mode = RubberColliderMode.Legacy;
			}
		}

		private void ResetToSplineSource()
		{
			_pathSource = RubberPathSource.Spline;
			_guideBindings = Array.Empty<RubberGuideBinding>();
			_bakedPath = Array.Empty<RubberPathElement>();
			_bakeVersion = 0;
			_bakeInputHash = default;
			_bakeFrameToLocal = Matrix4x4.identity;
			_restLength = 0f;
		}

		internal void RestorePackedState(RubberPathSource pathSource, RubberGuideBinding[] guideBindings,
			RubberPathElement[] bakedPath, uint bakeVersion, Hash128 bakeInputHash,
			Matrix4x4 bakeFrameToLocal, float restLength)
		{
			_pathSource = pathSource;
			_guideBindings = guideBindings ?? Array.Empty<RubberGuideBinding>();
			_bakedPath = bakedPath ?? Array.Empty<RubberPathElement>();
			_bakeVersion = bakeVersion;
			_bakeInputHash = bakeInputHash;
			_bakeFrameToLocal = bakeFrameToLocal;
			_restLength = math.max(0f, restLength);
		}

		internal void RestoreGuideReferences(RubberGuideComponent[] guides)
		{
			if (guides == null || guides.Length != GuideBindings.Count) {
				throw new ArgumentException("Guide reference count must match binding count.", nameof(guides));
			}
			for (var i = 0; i < guides.Length; i++) {
				_guideBindings[i].Guide = guides[i];
			}
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
	}
}
