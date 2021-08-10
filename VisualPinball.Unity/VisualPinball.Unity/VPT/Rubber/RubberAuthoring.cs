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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Rubber")]
	public class RubberAuthoring : ItemMainRenderableAuthoring<Rubber, RubberData>,
		IDragPointsAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float Height = 25f;

		public float HitHeight = 25f;

		public int Thickness = 8;

		public bool HitEvent;

		public float Elasticity;

		public float ElasticityFalloff;

		public float Friction;

		public float Scatter;

		public bool IsCollidable = true;

		public float RotX;

		public float RotY;

		public float RotZ;

		public bool OverwritePhysics;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

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

			Height = data.Height;
			HitHeight = data.HitHeight;
			Thickness = data.Thickness;
			HitEvent = data.HitEvent;
			Elasticity = data.Elasticity;
			ElasticityFalloff = data.ElasticityFalloff;
			Friction = data.Friction;
			Scatter = data.Scatter;
			IsCollidable = data.IsCollidable;
			RotX = data.RotX;
			RotY = data.RotY;
			RotZ = data.RotZ;
			OverwritePhysics = data.OverwritePhysics;
			DragPoints = data.DragPoints;

			return updatedComponents;
		}

		public override RubberData CopyDataTo(RubberData data, string[] materialNames, string[] textureNames)
		{
			var localRot = transform.localEulerAngles;

			// update the name
			data.Name = name;
			data.RotX = localRot.x;
			data.RotY = localRot.y;
			data.RotZ = localRot.z;

			// update visibility
			data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case RubberMeshAuthoring meshAuthoring:
						data.IsVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is RubberColliderAuthoring colliderAuthoring) {
					data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}

			// other props
			data.Height = Height;
			data.HitHeight = HitHeight;
			data.Thickness = Thickness;
			data.HitEvent = HitEvent;
			data.Elasticity = Elasticity;
			data.ElasticityFalloff = ElasticityFalloff;
			data.Friction = Friction;
			data.Scatter = Scatter;
			data.IsCollidable = IsCollidable;
			data.RotX = RotX;
			data.RotY = RotY;
			data.RotZ = RotZ;
			data.OverwritePhysics = OverwritePhysics;
			data.DragPoints = DragPoints;

			return data;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => DragPoints.Length == 0 ? Vector3.zero : DragPoints[0].Center.ToUnityVector3(Data.Height);
		public override void SetEditorPosition(Vector3 pos)
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return;
			}

			Data.Height = pos.z;
			pos.z = 0f;
			var diff = pos.ToVertex3D() - Data.DragPoints[0].Center;
			diff.Z = 0f;
			Data.DragPoints[0].Center = pos.ToVertex3D();
			for (var i = 1; i < Data.DragPoints.Length; i++) {
				var pt = Data.DragPoints[i];
				pt.Center += diff;
			}
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => transform.rotation.eulerAngles;
		public override void SetEditorRotation(Vector3 rot) => transform.rotation = Quaternion.Euler(rot);
	}
}
