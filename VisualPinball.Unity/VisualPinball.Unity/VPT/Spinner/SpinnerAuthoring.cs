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
		protected override SpinnerData InstantiateData() => new SpinnerData();

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

		public override IEnumerable<MonoBehaviour> SetData(SpinnerData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			Height = data.Height;
			Length = data.Length;
			Damping = data.Damping;
			AngleMax = data.AngleMax;
			AngleMin = data.AngleMin;
			Elasticity = data.Elasticity;
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);

			return updatedComponents;
		}

		public override SpinnerData CopyDataTo(SpinnerData data, string[] materialNames, string[] textureNames)
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
					case SpinnerBracketMeshAuthoring bracketMeshAuthoring:
						var bracketMeshAuthoringEnabled = bracketMeshAuthoring.gameObject.activeInHierarchy;
						data.IsVisible = data.IsVisible || bracketMeshAuthoringEnabled;
						data.ShowBracket = bracketMeshAuthoringEnabled;
						break;

					case SpinnerPlateMeshAuthoring plateMeshAuthoring:
						data.IsVisible = plateMeshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision: spinners are always collidable

			// other props
			data.Height = Height;
			data.Length = Length;
			data.Damping = Damping;
			data.AngleMax = AngleMax;
			data.AngleMin = AngleMin;
			data.Elasticity = Elasticity;
			data.Surface = Surface ? Surface.name : string.Empty;

			return data;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos)
		{
			Data.Center = pos.ToVertex2Dxy();
			Data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(transform.localEulerAngles.x, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => transform.rotation = Quaternion.Euler(rot);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Length = scale.x;
	}
}
