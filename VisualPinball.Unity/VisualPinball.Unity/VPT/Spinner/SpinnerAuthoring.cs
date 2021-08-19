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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Spinner")]
	public class SpinnerAuthoring : ItemMainRenderableAuthoring<Spinner, SpinnerData>,
		ISwitchAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the spinner on the playfield.")]
		public Vector2 Position;

		public float Height = 60f;

		[Range(-180f, 180f)]
		[Tooltip("Z-Axis rotation of the spinner on the playfield.")]
		public float Rotation;

		[Min(0)]
		[Tooltip("Overall scaling of the spinner")]
		public float Length = 80f;

		[Range(0, 1f)]
		[Tooltip("Damping on each turn while moving.")]
		public float Damping = 0.9879f;

		[Range(-180f, 180f)]
		[Tooltip("Maximal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMax;

		[Range(-180f, 180f)]
		[Tooltip("Minimal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMin;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this spinner is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		#endregion

		public override ItemType ItemType => ItemType.Spinner;

		public bool IsPulseSwitch => true;

		public float HeightOnPlayfield => Height + (Surface?.Height(Position) ?? TableHeight);

		protected override Spinner InstantiateItem(SpinnerData data) => new Spinner(data);
		protected override SpinnerData InstantiateData() => new SpinnerData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Spinner, SpinnerData, SpinnerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Spinner, SpinnerData, SpinnerAuthoring>);

		private const string BracketMeshName = "Spinner (Bracket)";

		public override IEnumerable<Type> ValidParents => SpinnerColliderAuthoring.ValidParentTypes
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// physics collision data
			var collComponent = GetComponent<SpinnerColliderAuthoring>();
			if (collComponent) {

				dstManager.AddComponentData(entity, new SpinnerStaticData {
					AngleMax = math.radians(AngleMax),
					AngleMin = math.radians(AngleMin),
					Damping = math.pow(Damping, (float)PhysicsConstants.PhysFactor),
					Elasticity = collComponent.Elasticity,
					Height = Height
				});

				// enable animation if component available
				if (GetComponentInChildren<SpinnerPlateAnimationAuthoring>()) {
					dstManager.AddComponentData(entity, new SpinnerMovementData {
						Angle = math.radians(math.clamp(0.0f, Data.AngleMin, Data.AngleMax)),
						AngleSpeed = 0f
					});
				}
			}

			// register
			transform.GetComponentInParent<Player>().RegisterSpinner(Item, entity, ParentEntity, gameObject);
		}

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, HeightOnPlayfield);

			// scale
			t.localScale = new Vector3(Length, Length, Length);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		public override IEnumerable<MonoBehaviour> SetData(SpinnerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityFloat2();
			Height = data.Height;
			Length = data.Length;
			Rotation = data.Rotation;

			// spinner props
			Damping = data.Damping;
			AngleMax = data.AngleMax;
			AngleMin = data.AngleMin;

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.sharedMesh.name) {
					case BracketMeshName:
						mf.gameObject.SetActive(data.IsVisible && data.ShowBracket);
						break;
					default:
						mf.gameObject.SetActive(data.IsVisible);
						break;
				}
			}

			// collider data
			var collComponent = GetComponent<SpinnerColliderAuthoring>();
			if (collComponent) {
				collComponent.Elasticity = data.Elasticity;
				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(SpinnerData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			return Array.Empty<MonoBehaviour>();
		}

		public override SpinnerData CopyDataTo(SpinnerData data, string[] materialNames, string[] textureNames)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Height = Height;
			data.Length = Length;
			data.Rotation = Rotation;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// spinner props
			data.Damping = Damping;
			data.AngleMax = AngleMax;
			data.AngleMin = AngleMin;

			// visibility
			var isBracketActive = false;
			var isAnythingElseActive = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.sharedMesh.name) {
					case BracketMeshName:
						isBracketActive = mf.gameObject.activeInHierarchy;
						break;
					default:
						isAnythingElseActive = isAnythingElseActive || mf.gameObject.activeInHierarchy;
						break;
				}
			}
			data.IsVisible = isAnythingElseActive || isBracketActive;
			data.ShowBracket = isBracketActive;

			var collComponent = GetComponent<SpinnerColliderAuthoring>();
			if (collComponent) {
				data.Elasticity = collComponent.Elasticity;
			}

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
