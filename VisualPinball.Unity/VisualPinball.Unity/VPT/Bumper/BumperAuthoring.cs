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
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Bumper")]
	public class BumperAuthoring : ItemMainRenderableAuthoring<Bumper, BumperData>,
		ISwitchAuthoring, ICoilAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float Radius = 45f;

		public float Threshold = 1.0f;

		public float Force = 15f;

		public float Scatter;

		public float RingSpeed = 0.5f;

		public float RingDropOffset = 0.0f;

		public SurfaceAuthoring Surface;

		public bool HitEvent = true;

		#endregion
		protected override Bumper InstantiateItem(BumperData data) => new Bumper(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Bumper, BumperData, BumperAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Bumper, BumperData, BumperAuthoring>);

		public override IEnumerable<Type> ValidParents => BumperBaseMeshAuthoring.ValidParentTypes
			.Concat(BumperCapMeshAuthoring.ValidParentTypes)
			.Concat(BumperRingMeshAuthoring.ValidParentTypes)
			.Concat(BumperSkirtMeshAuthoring.ValidParentTypes)
			.Concat(BumperColliderAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			dstManager.AddComponentData(entity, new BumperStaticData {
				Force = Force,
				HitEvent = HitEvent,
				Threshold = Threshold
			});

			var table = Table;
			var bumper = Item;

			// add ring data
			if (GetComponentInChildren<BumperRingAnimationAuthoring>()) {
				dstManager.AddComponentData(entity, new BumperRingAnimationData {

					// dynamic
					IsHit = false,
					Offset = 0,
					AnimateDown = false,
					DoAnimate = false,

					// static
					DropOffset = RingDropOffset,
					HeightScale = transform.localScale.z,
					Speed = RingSpeed,
					ScaleZ = table.GetScaleZ()
				});
			}

			// add ring data
			if (GetComponentInChildren<BumperSkirtAnimationAuthoring>()) {
				dstManager.AddComponentData(entity, new BumperSkirtAnimationData {
					BallPosition = default,
					AnimationCounter = 0f,
					DoAnimate = false,
					DoUpdate = false,
					EnableAnimation = true,
					Rotation = new float2(0, 0),
					HitEvent = bumper.Data.HitEvent,
					Center = bumper.Data.Center.ToUnityFloat2()
				});
			}

			transform.GetComponentInParent<Player>().RegisterBumper(Item, entity, ParentEntity, gameObject);
		}

		public override void SetData(BumperData data, Dictionary<string, IItemMainAuthoring> itemMainAuthorings)
		{
			Radius = data.Radius;
			Threshold = data.Threshold;
			Force = data.Force;
			Scatter = data.Scatter;
			RingSpeed = data.RingSpeed;
			RingDropOffset = data.RingDropOffset;
			Surface = GetAuthoring<SurfaceAuthoring>(itemMainAuthorings, data.Surface);
			HitEvent = data.HitEvent;
			transform.localScale = new Vector3(1f, 1f, data.HeightScale);
		}

		public override void CopyDataTo(BumperData data)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Center = localPos.ToVertex2Dxy();

			// update visibility
			data.IsBaseVisible = false;
			data.IsCapVisible = false;
			data.IsRingVisible = false;
			data.IsSocketVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case BumperBaseMeshAuthoring baseMeshAuthoring:
						data.IsCapVisible = baseMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case BumperCapMeshAuthoring capMeshAuthoring:
						data.IsCapVisible = capMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case BumperRingMeshAuthoring ringMeshAuthoring:
						data.IsRingVisible = ringMeshAuthoring.gameObject.activeInHierarchy;
						break;
					case BumperSkirtMeshAuthoring skirtMeshAuthoring:
						data.IsSocketVisible = skirtMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is BumperColliderAuthoring colliderAuthoring) {
					data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}

			// other props
			data.Radius = Radius;
			data.Threshold = Threshold;
			data.Force = Force;
			data.Scatter = Scatter;
			data.RingSpeed = RingSpeed;
			data.RingDropOffset = RingDropOffset;
			data.Surface = Surface ? Surface.name : string.Empty;
			data.HitEvent = HitEvent;
			data.HeightScale = transform.localScale.z;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.Orientation, 0, 0);
		public override void SetEditorRotation(Vector3 rot) => Data.Orientation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Data.Radius, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Data.Radius = scale.x;
	}
}
