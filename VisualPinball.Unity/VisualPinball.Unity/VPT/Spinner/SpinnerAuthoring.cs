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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Spinner")]
	public class SpinnerAuthoring : ItemMainRenderableAuthoring<Spinner, SpinnerData>,
		ISwitchAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float Height = 60f;

		public float Length = 80f;

		public float Damping = 0.9879f;

		public float AngleMax;

		public float AngleMin;

		public float Elasticity = 0.3f;

		public SurfaceAuthoring Surface;

		#endregion

		protected override Spinner InstantiateItem(SpinnerData data) => new Spinner(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Spinner, SpinnerData, SpinnerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Spinner, SpinnerData, SpinnerAuthoring>);

		public override IEnumerable<Type> ValidParents => SpinnerColliderAuthoring.ValidParentTypes
			.Concat(SpinnerBracketMeshAuthoring.ValidParentTypes)
			.Concat(SpinnerPlateMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new SpinnerStaticData {
				AngleMax = math.radians(Data.AngleMax),
				AngleMin = math.radians(Data.AngleMin),
				Damping = math.pow(Data.Damping, (float)PhysicsConstants.PhysFactor),
				Elasticity = Data.Elasticity,
				Height = Data.Height
			});

			if (GetComponentInChildren<SpinnerPlateAnimationAuthoring>()) {
				dstManager.AddComponentData(entity, new SpinnerMovementData {
					Angle = math.radians(math.clamp(0.0f, Data.AngleMin, Data.AngleMax)),
					AngleSpeed = 0f
				});
			}

			// register
			transform.GetComponentInParent<Player>().RegisterSpinner(Item, entity, ParentEntity, gameObject);
		}

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// update visibility
			Data.IsVisible = false;
			Data.ShowBracket = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case SpinnerBracketMeshAuthoring bracketMeshAuthoring:
						var bracketMeshAuthoringEnabled = bracketMeshAuthoring.gameObject.activeInHierarchy;
						Data.IsVisible = Data.IsVisible || bracketMeshAuthoringEnabled;
						Data.ShowBracket = bracketMeshAuthoringEnabled;
						break;

					case SpinnerPlateMeshAuthoring plateMeshAuthoring:
						Data.IsVisible = plateMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision: spinners are always collidable
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos)
		{
			Data.Center = pos.ToVertex2Dxy();
			Data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Data.Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Data.Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Data.Length = scale.x;
	}
}
