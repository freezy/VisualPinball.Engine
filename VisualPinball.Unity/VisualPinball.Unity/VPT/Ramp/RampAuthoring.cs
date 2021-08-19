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
	[AddComponentMenu("Visual Pinball/Game Item/Ramp")]
	public class RampAuthoring : ItemMainRenderableAuthoring<Ramp, RampData>, ISurfaceAuthoring, IDragPointsAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Choose between a flat ramp or various wire ramps.")]
		public int Type = RampType.RampTypeFlat;

		[Tooltip("The bottom height of the ramp.")]
		public float HeightBottom;

		[Tooltip("The top height of the ramp.")]
		public float HeightTop = 50f;

		[Tooltip("Defines how the UVs are generated. Setting it to world means the UVs are aligned with those of the playfield.")]
		public int ImageAlignment = RampImageAlignment.ImageModeWorld;

		[Min(0)]
		[Tooltip("Rendered height of the left wall.")]
		public float LeftWallHeightVisible = 30f;

		[Min(0)]
		[Tooltip("Rendered height of the right wall.")]
		public float RightWallHeightVisible = 30f;

		[Min(0)]
		[Tooltip("Width at the bottom of the ramp.")]
		public float WidthBottom = 75f;

		[Min(0)]
		[Tooltip("Width at the top of the ramp.")]
		public float WidthTop = 60f;

		[Min(0)]
		[Tooltip("Diameter of the wires.")]
		public float WireDiameter = 8f;

		[Min(0)]
		[Tooltip("Horizontal distance between the wires.")]
		public float WireDistanceX = 38f;

		[Min(0)]
		[Tooltip("Vertical distance between the wires.")]
		public float WireDistanceY = 88f;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		public override ItemType ItemType => ItemType.Ramp;

		protected override Ramp InstantiateItem(RampData data) => new Ramp(data);
		protected override RampData InstantiateData() => new RampData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Ramp, RampData, RampAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Ramp, RampData, RampAuthoring>);

		public override IEnumerable<Type> ValidParents => RampColliderAuthoring.ValidParentTypes
			.Concat(RampFloorMeshAuthoring.ValidParentTypes)
			.Concat(RampWallMeshAuthoring.ValidParentTypes)
			.Concat(RampWireMeshAuthoring.ValidParentTypes)
			.Distinct();

		public float Height(Vector2 pos) => 0f; // todo

		public bool IsWireRamp => Type != RampType.RampTypeFlat;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRamp(Item, entity, ParentEntity, gameObject);
		}

		public override void UpdateVisibility()
		{
			// visibility
			var wallComponent = GetComponentInChildren<RampWallMeshAuthoring>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshAuthoring>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshAuthoring>(true);
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
				if (wallComponent) wallComponent.gameObject.SetActive(isVisible && (LeftWallHeightVisible > 0 || RightWallHeightVisible > 0));
			}
		}

		public override IEnumerable<MonoBehaviour> SetData(RampData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry
			DragPoints = data.DragPoints;
			HeightTop = data.HeightTop;
			HeightBottom = data.HeightBottom;
			WidthTop = data.WidthTop;
			WidthBottom = data.WidthBottom;
			LeftWallHeightVisible = data.LeftWallHeightVisible;
			RightWallHeightVisible = data.RightWallHeightVisible;

			// type and uvs
			Type = data.RampType;
			ImageAlignment = data.ImageAlignment;

			// wire data
			WireDiameter = data.WireDiameter;
			WireDistanceX = data.WireDistanceX;
			WireDistanceY = data.WireDistanceY;

			// visibility and mesh creation
			var wallComponent = GetComponentInChildren<RampWallMeshAuthoring>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshAuthoring>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshAuthoring>(true);
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
					wallComponent.gameObject.SetActive(data.IsVisible && (LeftWallHeightVisible > 0 || RightWallHeightVisible > 0));
				}
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderAuthoring>();
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

		public override IEnumerable<MonoBehaviour> SetReferencedData(RampData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			// meshes
			var wallComponent = GetComponentInChildren<RampWallMeshAuthoring>(true);
			var floorComponent = GetComponentInChildren<RampFloorMeshAuthoring>(true);
			var wireComponent = GetComponentInChildren<RampWireMeshAuthoring>(true);
			if (wireComponent) {
				wireComponent.CreateMesh(data, textureProvider, materialProvider);
			}
			if (floorComponent) {
				floorComponent.CreateMesh(data, textureProvider, materialProvider);
			}
			if (wallComponent) {
				wallComponent.CreateMesh(data, textureProvider, materialProvider);
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderAuthoring>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override RampData CopyDataTo(RampData data, string[] materialNames, string[] textureNames)
		{
			// update the name
			data.Name = name;

			// geometry
			data.DragPoints = DragPoints;
			data.HeightTop = HeightTop;
			data.HeightBottom = HeightBottom;
			data.WidthTop = WidthTop;
			data.WidthBottom = WidthBottom;
			data.LeftWallHeightVisible = LeftWallHeightVisible;
			data.RightWallHeightVisible = RightWallHeightVisible;

			// type and uvs
			data.RampType = Type;
			data.ImageAlignment = ImageAlignment;

			// wire data
			data.WireDiameter = WireDiameter;
			data.WireDistanceX = WireDistanceX;
			data.WireDistanceY = WireDistanceY;

			// visibility
			var floorComponent = GetComponentInChildren<RampFloorMeshAuthoring>();
			var wireComponent = GetComponentInChildren<RampWireMeshAuthoring>();
			if (IsWireRamp) {
				data.IsVisible = wireComponent && wireComponent.gameObject.activeInHierarchy;
			} else {
				data.IsVisible = floorComponent && floorComponent.gameObject.activeInHierarchy;
			}

			// collider data
			var collComponent = GetComponentInChildren<RampColliderAuthoring>();
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
		}

		#endregion
	}
}
