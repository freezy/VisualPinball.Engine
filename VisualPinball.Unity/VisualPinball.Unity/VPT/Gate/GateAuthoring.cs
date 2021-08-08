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
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Gate")]
	public class GateAuthoring : ItemMainRenderableAuthoring<Gate, GateData>,
		ISwitchAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the gate on the playfield.")]
		public Vector3 Position;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the gate on the playfield (z-axis rotation)")]
		public float Rotation;

		[Range(10f, 250f)]
		[Tooltip("How much the gate is scaled, in percent.")]
		public float Length = 100f;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this flipper is attached to. Updates z translation.")]
		public MonoBehaviour _surface;

		#endregion

		protected override Gate InstantiateItem(GateData data) => new Gate(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Gate, GateData, GateAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Gate, GateData, GateAuthoring>);

		private const string BracketPrefabName = "Bracket";
		private const string WirePrefabName = "Wire";

		public override IEnumerable<Type> ValidParents => GateColliderAuthoring.ValidParentTypes
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// collision
			var colliderAuthoring = gameObject.GetComponent<GateColliderAuthoring>();
			if (colliderAuthoring) {

				dstManager.AddComponentData(entity, new GateStaticData {
					AngleMin = math.radians(colliderAuthoring.AngleMin),
					AngleMax = math.radians(colliderAuthoring.AngleMax),
					Height = Position.z,
					Damping = math.pow(colliderAuthoring.Damping, (float)PhysicsConstants.PhysFactor),
					GravityFactor = colliderAuthoring.GravityFactor,
					TwoWay = colliderAuthoring.TwoWay
				});

				// movement data
				if (GetComponentInChildren<GateWireAnimationAuthoring>()) {
					dstManager.AddComponentData(entity, new GateMovementData {
						Angle = math.radians(colliderAuthoring.AngleMin),
						AngleSpeed = 0,
						ForcedMove = false,
						IsOpen = false,
						HitDirection = false
					});
				}
			}

			// register
			transform.GetComponentInParent<Player>().RegisterGate(Item, entity, ParentEntity, gameObject);
		}

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = Surface != null
				? Position + new Vector3(0, 0, Surface.Height(Position))
				: Position;

			// scale
			t.localScale = new Vector3(Length, Length, Length);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		public override IEnumerable<MonoBehaviour> SetData(GateData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector3(data.Height);
			Rotation = data.Rotation > 180f ? data.Rotation - 360f : data.Rotation;
			Length = data.Length;
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			UpdateTransforms();

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketPrefabName:
						mf.gameObject.SetActive(data.IsVisible && data.ShowBracket);
						break;
					default:
						mf.gameObject.SetActive(data.IsVisible);
						break;
				}
			}

			// collider data
			var colliderAuthoring = gameObject.GetComponent<GateColliderAuthoring>();
			if (colliderAuthoring) {
				colliderAuthoring.AngleMin = math.degrees(data.AngleMin);
				colliderAuthoring.AngleMax = math.degrees(data.AngleMax);
				if (colliderAuthoring.AngleMin > 180f) {
					colliderAuthoring.AngleMin -= 360f;
				}
				if (colliderAuthoring.AngleMax > 180f) {
					colliderAuthoring.AngleMax -= 360f;
				}
				colliderAuthoring.Damping = data.Damping;
				colliderAuthoring.Elasticity = data.Elasticity;
				colliderAuthoring.Friction = data.Friction;
				colliderAuthoring.GravityFactor = data.GravityFactor;
				colliderAuthoring.TwoWay = data.TwoWay;

				updatedComponents.Add(colliderAuthoring);
			}

			return updatedComponents;
		}

		public override GateData CopyDataTo(GateData data)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2Dxy();
			data.Rotation = Rotation;
			data.Height = Position.z;
			data.Length = Length;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketPrefabName:
						data.ShowBracket = mf.gameObject.activeInHierarchy;
						break;
					case WirePrefabName:
						data.IsVisible = mf.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision data
			var colliderAuthoring = gameObject.GetComponent<GateColliderAuthoring>();
			if (colliderAuthoring) {
				data.IsCollidable = true;

				data.AngleMin = math.radians(colliderAuthoring.AngleMin);
				data.AngleMax = math.radians(colliderAuthoring.AngleMax);
				data.Damping = colliderAuthoring.Damping;
				data.Elasticity = colliderAuthoring.Elasticity;
				data.Friction = colliderAuthoring.Friction;
				data.GravityFactor = colliderAuthoring.GravityFactor;
				data.TwoWay = colliderAuthoring.TwoWay;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Length = scale.x;

		#endregion
	}
}
