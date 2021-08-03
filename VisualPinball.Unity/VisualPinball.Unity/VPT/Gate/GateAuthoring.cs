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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Gate")]
	public class GateAuthoring : ItemMainRenderableAuthoring<Gate, GateData>,
		ISwitchAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float Rotation;

		public float AngleMax = math.PI / 2.0f;

		public float AngleMin;

		public float Damping = 0.985f;

		public float Elasticity = 0.3f;

		public float Friction = 0.02f;

		public float GravityFactor = 0.25f;

		public float Height = 50f;

		public bool IsCollidable = true;

		public float Length = 100f;

		public SurfaceAuthoring Surface;

		public bool TwoWay;

		#endregion
		protected override Gate InstantiateItem(GateData data) => new Gate(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Gate, GateData, GateAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Gate, GateData, GateAuthoring>);

		public override IEnumerable<Type> ValidParents => GateColliderAuthoring.ValidParentTypes
			.Concat(GateBracketMeshAuthoring.ValidParentTypes)
			.Concat(GateWireMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new GateStaticData {
				AngleMin = Data.AngleMin,
				AngleMax = Data.AngleMax,
				Height = Data.Height,
				Damping = math.pow(Data.Damping, (float)PhysicsConstants.PhysFactor),
				GravityFactor = Data.GravityFactor,
				TwoWay = Data.TwoWay
			});

			// add movement data
			if (GetComponentInChildren<GateWireAnimationAuthoring>()) {
				dstManager.AddComponentData(entity, new GateMovementData {
					Angle = Data.AngleMin,
					AngleSpeed = 0,
					ForcedMove = false,
					IsOpen = false,
					HitDirection = false
				});
			}

			// register
			transform.GetComponentInParent<Player>().RegisterGate(Item, entity, ParentEntity, gameObject);
		}

		public override void SetData(GateData data, Dictionary<string, IItemMainAuthoring> components)
		{
			Rotation = data.Rotation;
			AngleMax = data.AngleMax;
			AngleMin = data.AngleMin;
			Damping = data.Damping;
			Elasticity = data.Elasticity;
			Friction = data.Friction;
			GravityFactor = data.GravityFactor;
			Height = data.Height;
			IsCollidable = data.IsCollidable;
			Length = data.Length;
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			TwoWay = data.TwoWay;
		}

		public override void CopyDataTo(GateData data)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Center = localPos.ToVertex2Dxy();

			// update visibility
			data.IsVisible = false;
			data.ShowBracket = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case GateBracketMeshAuthoring bracketMeshAuthoring:
						var bracketMeshAuthoringEnabled = bracketMeshAuthoring.gameObject.activeInHierarchy;
						data.IsVisible = data.IsVisible || bracketMeshAuthoringEnabled;
						data.ShowBracket = bracketMeshAuthoringEnabled;
						break;

					case GateWireMeshAuthoring wireMeshAuthoring:
						data.IsVisible = wireMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is GateColliderAuthoring colliderAuthoring) {
					data.IsCollidable = colliderAuthoring.gameObject.activeInHierarchy;
				}
			}

			// other props
			data.Rotation = Rotation;
			data.AngleMax = AngleMax;
			data.AngleMin = AngleMin;
			data.Damping = Damping;
			data.Elasticity = Elasticity;
			data.Friction = Friction;
			data.GravityFactor = GravityFactor;
			data.Height = Height;
			data.IsCollidable = IsCollidable;
			data.Length = Length;
			data.Surface = Surface ? Surface.name : string.Empty;
			data.TwoWay = TwoWay;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos)
		{
			Data.Center = pos.ToVertex2Dxy();
			Data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Length = scale.x;
	}
}
