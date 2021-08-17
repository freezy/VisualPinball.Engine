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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Rubber")]
	public class RubberAuthoring : ItemMainRenderableAuthoring<Rubber, RubberData>,
		IDragPointsAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Height of the rubber (z-axis).")]
		public float Height = 25f;

		public float HitHeight = 25f;

		[Min(0)]
		[Tooltip("How thick the rubber band is rendered.")]
		public int Thickness = 8;

		[Tooltip("Rotation on the playfield")]
		public Vector3 Rotation;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		public override ItemType ItemType => ItemType.Rubber;

		protected override Rubber InstantiateItem(RubberData data) => new Rubber(data);
		protected override RubberData InstantiateData() => new RubberData();
		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Rubber, RubberData, RubberAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Rubber, RubberData, RubberAuthoring>);

		public override IEnumerable<Type> ValidParents => RubberColliderAuthoring.ValidParentTypes
			.Concat(RubberMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRubber(Item, entity, ParentEntity, gameObject);
		}

		public override IEnumerable<MonoBehaviour> SetData(RubberData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry
			Height = data.Height;
			HitHeight = data.HitHeight;
			Rotation = new Vector3(data.RotX, data.RotY, data.RotZ);
			Thickness = data.Thickness;
			DragPoints = data.DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				mr.enabled = data.IsVisible;
			}

			// collider data
			var collComponent = GetComponentInChildren<RubberColliderAuthoring>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;

				collComponent.HitEvent = data.HitEvent;
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
				collComponent.OverwritePhysics = data.OverwritePhysics;
				collComponent.Elasticity = data.Elasticity;
				collComponent.ElasticityFalloff = data.ElasticityFalloff;
				collComponent.Friction = data.Friction;
				collComponent.Scatter = data.Scatter;

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override RubberData CopyDataTo(RubberData data, string[] materialNames, string[] textureNames)
		{
			// update the name
			data.Name = name;

			// geometry
			data.Height = Height;
			data.HitHeight = HitHeight;
			data.RotX = Rotation.x;
			data.RotY = Rotation.y;
			data.RotZ = Rotation.z;
			data.Thickness = Thickness;
			data.DragPoints = DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			data.IsVisible = mr && mr.enabled;

			// collision
			var collComponent = GetComponentInChildren<RubberColliderAuthoring>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;

				data.HitEvent = collComponent.HitEvent;

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

		#region Editor Tooling

		private Vector3 DragPointCenter {
			get {
				var sum = Vertex3D.Zero;
				foreach (var t in DragPoints) {
					sum += t.Center;
				}
				var center = sum / DragPoints.Length;
				return center.ToUnityVector3();
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition()
		{
			var pos = DragPoints.Length == 0 ? Vector3.zero : DragPointCenter;
			return new Vector3(pos.x, pos.y, HitHeight);
		}
		public override void SetEditorPosition(Vector3 pos) {
			if (DragPoints.Length == 0) {
				return;
			}
			var diff = (pos - DragPointCenter).ToVertex3D();
			diff.Z = 0f;
			foreach (var pt in DragPoints) {
				pt.Center += diff;
			}
			HitHeight = pos.z;
			RebuildMeshes();
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => Rotation;
		public override void SetEditorRotation(Vector3 rot) {
			Rotation = rot;
			RebuildMeshes();
		}

		#endregion
	}
}
