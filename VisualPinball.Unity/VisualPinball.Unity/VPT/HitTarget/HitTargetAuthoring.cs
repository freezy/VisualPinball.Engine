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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Hit Target")]
	public class HitTargetAuthoring : ItemMainRenderableAuthoring<HitTarget, HitTargetData>,
		ISwitchAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float RotZ;

		public Vector3 Size;

		public float DepthBias;

		public float DropSpeed =  0.5f;

		public int RaiseDelay = 100;

		public float Elasticity;

		public float ElasticityFalloff;

		public float Friction;

		public bool IsCollidable = true;

		public bool IsDropped;

		public bool IsLegacy;

		public bool OverwritePhysics;

		public float Scatter;

		public string PhysicsMaterial = string.Empty;

		public float Threshold = 2.0f;

		public bool UseHitEvent = true;

		#endregion

		protected override HitTarget InstantiateItem(HitTargetData data) => new HitTarget(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<HitTarget, HitTargetData, HitTargetAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<HitTarget, HitTargetData, HitTargetAuthoring>);

		public override IEnumerable<Type> ValidParents => HitTargetColliderAuthoring.ValidParentTypes
			.Concat(HitTargetMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;

			dstManager.AddComponentData(entity, new HitTargetStaticData {
				TargetType = Data.TargetType,
				DropSpeed = Data.DropSpeed,
				RaiseDelay = Data.RaiseDelay,
				UseHitEvent = Data.UseHitEvent,
				RotZ = Data.RotZ,
				TableScaleZ = table.GetScaleZ()
			});
			dstManager.AddComponentData(entity, new HitTargetAnimationData {
				IsDropped = Data.IsDropped
			});
			dstManager.AddComponentData(entity, new HitTargetMovementData());

			// register
			var hitTarget = transform.GetComponent<HitTargetAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterHitTarget(hitTarget, entity, ParentEntity, gameObject);
		}

		public override void SetData(HitTargetData data, IMaterialProvider materialProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Size = data.Size.ToUnityFloat3();
			RotZ = data.RotZ;
			DepthBias = data.DepthBias;
			DropSpeed = data.DropSpeed;
			RaiseDelay = data.RaiseDelay;
			Elasticity = data.Elasticity;
			ElasticityFalloff = data.ElasticityFalloff;
			Friction = data.Friction;
			IsCollidable = data.IsCollidable;
			IsDropped = data.IsDropped;
			IsLegacy = data.IsLegacy;
			OverwritePhysics = data.OverwritePhysics;
			Scatter = data.Scatter;
			PhysicsMaterial = data.PhysicsMaterial;
			Threshold = data.Threshold;
			UseHitEvent = data.UseHitEvent;
		}

		public override void CopyDataTo(HitTargetData data)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Position = localPos.ToVertex3D();

			// update visibility
			data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case HitTargetMeshAuthoring meshAuthoring:
						data.IsVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is HitTargetColliderAuthoring colliderAuthoring) {
					data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}

			// other props
			data.Size = Size.ToVertex3D();
			data.RotZ = RotZ;
			data.DepthBias = DepthBias;
			data.DropSpeed = DropSpeed;
			data.RaiseDelay = RaiseDelay;
			data.Elasticity = Elasticity;
			data.ElasticityFalloff = ElasticityFalloff;
			data.Friction = Friction;
			data.IsCollidable = IsCollidable;
			data.IsDropped = IsDropped;
			data.IsLegacy = IsLegacy;
			data.OverwritePhysics = OverwritePhysics;
			data.Scatter = Scatter;
			data.PhysicsMaterial = PhysicsMaterial;
			data.Threshold = Threshold;
			data.UseHitEvent = UseHitEvent;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(RotZ, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => RotZ = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => Size;
		public override void SetEditorScale(Vector3 scale) => Size = scale;
	}
}
