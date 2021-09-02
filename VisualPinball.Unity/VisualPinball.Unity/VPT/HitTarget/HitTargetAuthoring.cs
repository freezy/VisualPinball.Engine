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
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Hit Target")]
	public class HitTargetAuthoring : ItemMainRenderableAuthoring<HitTargetData>,
		ISwitchDeviceAuthoring, IHitTargetData, IMeshGenerator, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the target on the playfield.")]
		public Vector3 Position;

		[Range(-180f, 180f)]
		[Tooltip("Z-Axis rotation of the target.")]
		public float Rotation;

		[Tooltip("Overall scaling of the target.")]
		public Vector3 Size = new Vector3(32f, 32f, 32f);

		[Range(1, 9)]
		private int _targetType = Engine.VPT.TargetType.DropTargetBeveled;

		#endregion

		#region IHitTargetData

		public bool IsDropTarget {
			get {
				var comp = GetComponent<DropTargetAnimationAuthoring>();
				return comp && comp.isActiveAndEnabled;
			}
		}

		public bool IsLegacy {
			get {
				var colliderAuthoring = GetComponent<HitTargetColliderAuthoring>();
				return colliderAuthoring && colliderAuthoring.IsLegacy;
			}
		}

		public int TargetType => _targetType;

		public float RotZ => Rotation;
		public float ScaleX => Size.x;
		public float ScaleY => Size.y;
		public float ScaleZ => Size.z;
		public float PositionX => Position.x;
		public float PositionY => Position.y;
		public float PositionZ => Position.z;

		#endregion

		#region IMeshGenerator

		public Mesh GetMesh() => GetDefaultMesh();

		public Matrix3D GetTransformationMatrix()
		{
			var t = transform;
			return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale).ToVpMatrix();
		}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.HitTarget;
		public override string ItemName => "Target";

		public override IEnumerable<Type> ValidParents => HitTargetColliderAuthoring.ValidParentTypes
			.Distinct();

		public override HitTargetData InstantiateData() => new HitTargetData();
		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<HitTargetData, HitTargetAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<HitTargetData, HitTargetAuthoring>);

		public const string SwitchItem = "target_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { IsPulseSwitch = true }
		};
		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		IEnumerable<GamelogicEngineSwitch> IDeviceAuthoring<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation


		public override void UpdateTransforms()
		{
			var t = transform;
			t.localPosition = new Vector3(Position.x, Position.y, Position.z + PlayfieldHeight);
			t.localScale = Size;
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		#endregion

		#region Conversion

			public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var colliderAuthoring = GetComponent<HitTargetColliderAuthoring>();
			if (colliderAuthoring) {

				var hitTargetAnimationAuthoring = GetComponent<HitTargetAnimationAuthoring>();
				var dropTargetAnimationAuthoring = GetComponent<DropTargetAnimationAuthoring>();
				if (dropTargetAnimationAuthoring || hitTargetAnimationAuthoring) {

					if (hitTargetAnimationAuthoring) {
						dstManager.AddComponentData(entity, new HitTargetStaticData {
							Speed = hitTargetAnimationAuthoring.Speed,
							MaxAngle = hitTargetAnimationAuthoring.MaxAngle,
						});
						dstManager.AddComponentData(entity, new HitTargetAnimationData());
					}

					if (dropTargetAnimationAuthoring) {
						dstManager.AddComponentData(entity, new DropTargetStaticData {
							Speed = dropTargetAnimationAuthoring.Speed,
							RaiseDelay = dropTargetAnimationAuthoring.RaiseDelay,
							UseHitEvent = colliderAuthoring.UseHitEvent,
						});
						dstManager.AddComponentData(entity, new DropTargetAnimationData {
							IsDropped = dropTargetAnimationAuthoring.IsDropped
						});
					}
				}
			}

			// register
			transform.GetComponentInParent<Player>().RegisterHitTarget(this, entity, ParentEntity);
		}


		public override IEnumerable<MonoBehaviour> SetData(HitTargetData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Position.ToUnityVector3();
			Rotation = data.RotZ > 180f ? data.RotZ - 360f : data.RotZ;
			Size = data.Size.ToUnityVector3();

			_targetType = data.TargetType;

			// collider data
			var colliderAuthoring = GetComponent<HitTargetColliderAuthoring>();
			if (colliderAuthoring) {
				colliderAuthoring.enabled = data.IsCollidable;
				colliderAuthoring.UseHitEvent = data.UseHitEvent;
				colliderAuthoring.Threshold = data.Threshold;
				colliderAuthoring.IsLegacy = data.IsLegacy;

				colliderAuthoring.OverwritePhysics = data.OverwritePhysics;
				colliderAuthoring.Elasticity = data.Elasticity;
				colliderAuthoring.ElasticityFalloff = data.ElasticityFalloff;
				colliderAuthoring.Friction = data.Friction;
				colliderAuthoring.Scatter = data.Scatter;

				updatedComponents.Add(colliderAuthoring);

				// animation data
				var dropTargetAnimationAuthoring = GetComponent<DropTargetAnimationAuthoring>();
				if (dropTargetAnimationAuthoring) {
					dropTargetAnimationAuthoring.enabled = data.IsDropTarget;
					dropTargetAnimationAuthoring.Speed = data.DropSpeed;
					dropTargetAnimationAuthoring.RaiseDelay = data.RaiseDelay;
					dropTargetAnimationAuthoring.IsDropped = data.IsDropped;
					updatedComponents.Add(dropTargetAnimationAuthoring);
				}

				var hitTargetAnimationAuthoring = GetComponent<HitTargetAnimationAuthoring>();
				if (hitTargetAnimationAuthoring) {
					hitTargetAnimationAuthoring.enabled = !data.IsDropTarget;
					hitTargetAnimationAuthoring.Speed = data.DropSpeed;
					updatedComponents.Add(hitTargetAnimationAuthoring);
				}
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(HitTargetData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var colliderAuthoring = GetComponent<HitTargetColliderAuthoring>();
			if (colliderAuthoring) {
				colliderAuthoring.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}
			return Array.Empty<MonoBehaviour>();
		}

		public override HitTargetData CopyDataTo(HitTargetData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Position = Position.ToVertex3D();
			data.RotZ = Rotation;
			data.Size = Size.ToVertex3D();

			data.TargetType = _targetType;

			// collision data
			var colliderAuthoring = GetComponent<HitTargetColliderAuthoring>();
			if (colliderAuthoring) {
				data.IsCollidable = colliderAuthoring.enabled;
				data.Threshold = colliderAuthoring.Threshold;
				data.UseHitEvent = colliderAuthoring.UseHitEvent;
				data.PhysicsMaterial = colliderAuthoring.PhysicsMaterial == null ? string.Empty : colliderAuthoring.PhysicsMaterial.name;
				data.IsLegacy = colliderAuthoring.IsLegacy;

				data.OverwritePhysics = colliderAuthoring.OverwritePhysics;
				data.Elasticity = colliderAuthoring.Elasticity;
				data.ElasticityFalloff = colliderAuthoring.ElasticityFalloff;
				data.Friction = colliderAuthoring.Friction;
				data.Scatter = colliderAuthoring.Scatter;

				// animation data
				var dropTargetAnimationAuthoring = GetComponent<DropTargetAnimationAuthoring>();
				if (dropTargetAnimationAuthoring) {
					data.DropSpeed = dropTargetAnimationAuthoring.Speed;
					data.RaiseDelay = dropTargetAnimationAuthoring.RaiseDelay;
					data.IsDropped = dropTargetAnimationAuthoring.IsDropped;
				}

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => Position;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => Size;
		public override void SetEditorScale(Vector3 scale) => Size = scale;

		#endregion

	}
}
