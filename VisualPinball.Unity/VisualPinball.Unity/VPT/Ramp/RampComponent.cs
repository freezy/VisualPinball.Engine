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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Ramp")]
	public class RampComponent : ItemMainRenderableComponent<RampData>,
		IRampData, ISurfaceComponent, IDragPointsComponent, IConvertGameObjectToEntity
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
		public override IEnumerable<Type> ValidParents => RampColliderComponent.ValidParentTypes
			.Concat(RampFloorMeshComponent.ValidParentTypes)
			.Concat(RampWallMeshComponent.ValidParentTypes)
			.Concat(RampWireMeshComponent.ValidParentTypes)
			.Distinct();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshComponent<RampData, RampComponent>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderComponent<RampData, RampComponent>);

		public override void OnPlayfieldHeightUpdated() => RebuildMeshes();

		#endregion

		#region Transformation


		public float Height(Vector2 pos) {

			var vVertex = new RampMeshGenerator(this).GetCentralCurve();
			Engine.VPT.Mesh.ClosestPointOnPolygon(vVertex, new Vertex2D(pos.x, pos.y), false, out var vOut, out var iSeg);

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

			var topHeight = _heightTop + PlayfieldHeight;
			var bottomHeight = _heightBottom + PlayfieldHeight;

			return vVertex[iSeg].Z + startLength / totalLength * (topHeight - bottomHeight) + bottomHeight;
		}

		public override void UpdateVisibility()
		{
			// visibility
			var wallComponent = GetComponentInChildren<RampWallMeshComponent>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshComponent>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshComponent>(true);
			var isVisible = wireComponent && wireComponent.gameObject.activeInHierarchy ||
			                floorComponent && floorComponent.gameObject.activeInHierarchy;
			if (IsWireRamp) {
				if (wireComponent) wireComponent.gameObject.SetActive(isVisible);
				if (floorComponent) {
					floorComponent.gameObject.SetActive(false);
					floorComponent.ClearMeshVertices();
				}
				if (wallComponent) {
					wallComponent.gameObject.SetActive(false);
					wallComponent.ClearMeshVertices();
				}
			} else {
				if (wireComponent) {
					wireComponent.gameObject.SetActive(false);
					wireComponent.ClearMeshVertices();
				}
				if (floorComponent) floorComponent.gameObject.SetActive(isVisible);
				if (wallComponent) wallComponent.gameObject.SetActive(isVisible && (_leftWallHeightVisible > 0 || _rightWallHeightVisible > 0));
			}
		}

		#endregion

		#region Conversion

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRamp(this, entity, ParentEntity);
		}

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

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(RampData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainComponent> components)
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

		#endregion

		#region Editor Tooling

		private Vector3 DragPointCenter {
			get {
				var sum = Vertex3D.Zero;
				foreach (var t in DragPoints) {
					sum += t.Center;
				}
				var center = sum / DragPoints.Length;
				return new Vector3(center.X, center.Y, _heightTop);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition()
		{
			return DragPoints.Length == 0 ? Vector3.zero : DragPointCenter;
		}

		public override void SetEditorPosition(Vector3 pos)
		{
			if (DragPoints.Length == 0) {
				return;
			}
			var diff = (pos - DragPointCenter).ToVertex3D();
			diff.Z = 0f;
			foreach (var pt in DragPoints) {
				pt.Center += diff;
			}
			RebuildMeshes();
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			if (playfieldComponent) {
				WalkChildren(playfieldComponent.transform, UpdateSurfaceReferences);
			}
		}

		protected static void WalkChildren(IEnumerable node, Action<Transform> action)
		{
			foreach (Transform childTransform in node) {
				action(childTransform);
				WalkChildren(childTransform, action);
			}
		}

		protected void UpdateSurfaceReferences(Transform obj)
		{
			var surfaceAuthoring = obj.gameObject.GetComponent<IOnSurfaceComponent>();
			if (surfaceAuthoring != null && surfaceAuthoring.Surface == this) {
				surfaceAuthoring.OnSurfaceUpdated();
			}
		}

		#endregion
	}
}
