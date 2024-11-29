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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Table;
using MathF = VisualPinball.Engine.Math.MathF;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Ramp")]
	public class RampComponent : MainRenderableComponent<RampData>, IRampData
	{
		#region Data

		[Tooltip("Choose between a flat ramp or various wire ramps.")]
		public int _type = RampType.RampTypeFlat;

		[Tooltip("The bottom height of the ramp.")]
		public float _heightBottom;

		[Tooltip("The top height of the ramp.")]
		public float _heightTop = 50f;

		[Tooltip("Defines how the UVs are generated. Setting it to world means the UVs are aligned with those of the playfield.")]
		public int _imageAlignment = RampImageAlignment.ImageModeWorld;

		[Min(0)]
		[Tooltip("Rendered height of the left wall.")]
		public float _leftWallHeightVisible = 30f;

		[Min(0)]
		[Tooltip("Rendered height of the right wall.")]
		public float _rightWallHeightVisible = 30f;

		[Min(0)]
		[Tooltip("Width at the bottom of the ramp.")]
		public float _widthBottom = 75f;

		[Min(0)]
		[Tooltip("Width at the top of the ramp.")]
		public float _widthTop = 60f;

		[Min(0)]
		[Tooltip("Diameter of the wires.")]
		public float _wireDiameter = 8f;

		[Min(0)]
		[Tooltip("Horizontal distance between the wires.")]
		public float _wireDistanceX = 38f;

		[Min(0)]
		[Tooltip("Vertical distance between the wires.")]
		public float _wireDistanceY = 88f;

		[SerializeField]
		private DragPointData[] _dragPoints;

		#endregion

		#region IRampData

		public float HeightBottom => _heightBottom;
		public float HeightTop => _heightTop;
		public float RightWallHeightVisible => _rightWallHeightVisible;
		public float LeftWallHeightVisible => _leftWallHeightVisible;
		public int Type => _type;
		public float WireDistanceX => _wireDistanceX;
		public float WireDistanceY => _wireDistanceY;
		public int ImageAlignment => _imageAlignment;
		public float WireDiameter => _wireDiameter;
		public float WidthTop => _widthTop;
		public float WidthBottom => _widthBottom;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		public bool IsWireRamp => _type != RampType.RampTypeFlat;

		#region Overrides

		public override ItemType ItemType => ItemType.Ramp;
		public override string ItemName => "Ramp";

		public override RampData InstantiateData() => new RampData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<RampData, RampComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<RampData, RampComponent>);

		public override void OnPlayfieldHeightUpdated() => RebuildMeshes();

		#endregion

		#region Runtime

		public RampApi RampApi { get; private set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			RampApi = new RampApi(gameObject, Player, physicsEngine);

			Player.Register(RampApi, this);
			RegisterPhysics(physicsEngine);
		}

		private void Start()
		{
			_playfieldToWorld = Player.PlayfieldToWorldMatrix;
		}

		#endregion

		#region Transformation

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		public float Height(Vector2 pos) {

			var vVertex = new RampMeshGenerator(this).GetCentralCurve();
			Mesh.ClosestPointOnPolygon(vVertex, new Vertex2D(pos.x, pos.y), false, out var vOut, out var iSeg);

			if (iSeg == -1) {
				return 0.0f; // Object is not on ramp path
			}

			// Go through vertices (including iSeg itself) counting control points until iSeg
			var totalLength = 0.0f;
			var startLength = 0.0f;

			var cVertex = vVertex.Length;
			for (var i2 = 1; i2 < cVertex; i2++) {
				var vDx = vVertex[i2].X - vVertex[i2 - 1].X;
				var vDy = vVertex[i2].Y - vVertex[i2 - 1].Y;
				var vLen = MathF.Sqrt(vDx * vDx + vDy * vDy);
				if (i2 <= iSeg) {
					startLength += vLen;
				}
				totalLength += vLen;
			}

			var dx = vOut.X - vVertex[iSeg].X;
			var dy = vOut.Y - vVertex[iSeg].Y;
			var len = MathF.Sqrt(dx * dx + dy * dy);
			startLength += len; // Add the distance the object is between the two closest polyline segments.  Matters mostly for straight edges. Z does not respect that yet!

			var topHeight = _heightTop;
			var bottomHeight = _heightBottom;

			return vVertex[iSeg].Z + startLength / totalLength * (topHeight - bottomHeight) + bottomHeight;
		}

		// todo revisit
		// public override void UpdateVisibility()
		// {
		// 	// visibility
		// 	var wallComponent = GetComponentInChildren<RampWallMeshComponent>(true);
		// 	var floorComponent = GetComponentInChildren<RampFloorMeshComponent>(true);
		// 	var wireComponent = GetComponentInChildren<RampWireMeshComponent>(true);
		// 	var isVisible = wireComponent && wireComponent.gameObject.activeInHierarchy ||
		// 	                floorComponent && floorComponent.gameObject.activeInHierarchy;
		// 	if (IsWireRamp) {
		// 		if (wireComponent) wireComponent.gameObject.SetActive(isVisible);
		// 		if (floorComponent) {
		// 			floorComponent.gameObject.SetActive(false);
		// 			floorComponent.ClearMeshVertices();
		// 		}
		// 		if (wallComponent) {
		// 			wallComponent.gameObject.SetActive(false);
		// 			wallComponent.ClearMeshVertices();
		// 		}
		// 	} else {
		// 		if (wireComponent) {
		// 			wireComponent.gameObject.SetActive(false);
		// 			wireComponent.ClearMeshVertices();
		// 		}
		// 		if (floorComponent) floorComponent.gameObject.SetActive(isVisible);
		// 		if (wallComponent) wallComponent.gameObject.SetActive(isVisible && (_leftWallHeightVisible > 0 || _rightWallHeightVisible > 0));
		// 	}
		// }

		public float4x4 TransformationWithinPlayfield
			=> transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(RampData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry
			DragPoints = data.DragPoints;
			_heightTop = data.HeightTop;
			_heightBottom = data.HeightBottom;
			_widthTop = data.WidthTop;
			_widthBottom = data.WidthBottom;
			_leftWallHeightVisible = data.LeftWallHeightVisible;
			_rightWallHeightVisible = data.RightWallHeightVisible;

			// type and uvs
			_type = data.Type;
			_imageAlignment = data.ImageAlignment;

			// wire data
			_wireDiameter = data.WireDiameter;
			_wireDistanceX = data.WireDistanceX;
			_wireDistanceY = data.WireDistanceY;

			// visibility and mesh creation
			var wallComponent = GetComponentInChildren<RampWallMeshComponent>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshComponent>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshComponent>(true);
			if (IsWireRamp) {
				if (wireComponent) {
					wireComponent.gameObject.SetActive(data.IsVisible);
				}
				if (floorComponent) {
					floorComponent.gameObject.SetActive(false);
				}
				if (wallComponent) {
					wallComponent.gameObject.SetActive(false);
				}
			} else {
				if (wireComponent) {
					wireComponent.gameObject.SetActive(false);
				}
				if (floorComponent) {
					floorComponent.gameObject.SetActive(data.IsVisible);
				}
				if (wallComponent) {
					wallComponent.gameObject.SetActive(data.IsVisible && (_leftWallHeightVisible > 0 || _rightWallHeightVisible > 0));
				}
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderComponent>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;
				collComponent.Elasticity = data.Elasticity;
				collComponent.Friction = data.Friction;
				collComponent.HitEvent = data.HitEvent;
				collComponent.OverwritePhysics = data.OverwritePhysics;
				collComponent.Scatter = data.Scatter;
				collComponent.Threshold = data.Threshold;

				collComponent.LeftWallHeight = data.LeftWallHeight;
				collComponent.RightWallHeight = data.RightWallHeight;

				updatedComponents.Add(collComponent);
			}

			CenterPivot();

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(RampData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// meshes
			var wallComponent = GetComponentInChildren<RampWallMeshComponent>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshComponent>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshComponent>(true);
			if (wireComponent) {
				wireComponent.CreateMesh(data, table, textureProvider, materialProvider);
			}
			if (floorComponent) {
				floorComponent.CreateMesh(data, table, textureProvider, materialProvider);
			}
			if (wallComponent) {
				wallComponent.CreateMesh(data, table, textureProvider, materialProvider);
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderComponent>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override RampData CopyDataTo(RampData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// update the name
			data.Name = name;

			// geometry
			data.DragPoints = DragPoints;
			data.HeightTop = _heightTop;
			data.HeightBottom = _heightBottom;
			data.WidthTop = _widthTop;
			data.WidthBottom = _widthBottom;
			data.LeftWallHeightVisible = _leftWallHeightVisible;
			data.RightWallHeightVisible = _rightWallHeightVisible;

			// type and uvs
			data.RampType = _type;
			data.ImageAlignment = _imageAlignment;

			// wire data
			data.WireDiameter = _wireDiameter;
			data.WireDistanceX = _wireDistanceX;
			data.WireDistanceY = _wireDistanceY;

			// visibility
			var floorComponent = GetComponentInChildren<RampFloorMeshComponent>();
			var wireComponent = GetComponentInChildren<RampWireMeshComponent>();
			if (IsWireRamp) {
				data.IsVisible = wireComponent && wireComponent.gameObject.activeInHierarchy;
			} else {
				data.IsVisible = floorComponent && floorComponent.gameObject.activeInHierarchy;
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderComponent>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;

				data.LeftWallHeight = collComponent.LeftWallHeight;
				data.RightWallHeight = collComponent.RightWallHeight;

				data.HitEvent = collComponent.HitEvent;
				data.Threshold = collComponent.Threshold;
				data.PhysicsMaterial = collComponent.PhysicsMaterial ? collComponent.PhysicsMaterial.name : string.Empty;

				data.OverwritePhysics = collComponent.OverwritePhysics;
				data.Elasticity = collComponent.Elasticity;
				data.Friction = collComponent.Friction;
				data.Scatter = collComponent.Scatter;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var rampComponent = go.GetComponent<RampComponent>();
			if (rampComponent != null) {
				_type = rampComponent._type;
				_heightBottom = rampComponent._heightBottom;
				_heightTop = rampComponent._heightTop;
				_imageAlignment = rampComponent._imageAlignment;
				_leftWallHeightVisible = rampComponent._leftWallHeightVisible;
				_rightWallHeightVisible = rampComponent._rightWallHeightVisible;
				_widthBottom = rampComponent._widthBottom;
				_widthTop = rampComponent._widthTop;
				_wireDiameter = rampComponent._wireDiameter;
				_wireDistanceX = rampComponent._wireDistanceX;
				_wireDistanceY = rampComponent._wireDistanceY;
				_dragPoints = rampComponent._dragPoints.Select(dp => dp.Clone()).ToArray();

			} else {
				MoveDragPointsTo(_dragPoints, go.transform.localPosition.TranslateToVpx());
			}

			UpdateTransforms();
			RebuildMeshes();
		}

		private void CenterPivot()
		{
			var centerVpx = DragPoints.Aggregate(Vector3.zero, (current, dragPoint) => current + dragPoint.Center.ToUnityVector3());
			centerVpx /= DragPoints.Length;

			transform.Translate(centerVpx.TranslateToWorld(transform) - transform.position);
			foreach (var dragPoint in DragPoints) {
				dragPoint.Center -= centerVpx.ToVertex3D();
			}
			RebuildMeshes();
		}

		#endregion

	}
}
