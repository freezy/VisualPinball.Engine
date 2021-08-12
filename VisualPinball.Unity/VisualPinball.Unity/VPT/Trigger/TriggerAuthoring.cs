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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Trigger")]
	public class TriggerAuthoring : ItemMainRenderableAuthoring<Trigger, TriggerData>,
		ISwitchAuthoring, ITriggerAuthoring, IDragPointsAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position on the playfield.")]
		public Vector2 Position;

		[Tooltip("Rotation of the trigger.")]
		[Range(-180f, 180f)]
		public float Rotation;

		[Min(0)]
		[Tooltip("Radius of the trigger.")]
		public float Radius = 25f;

		[Min(0)]
		[Tooltip("Thickness of the trigger wire. Doesn't have any impact on the ball.")]
		public float WireThickness;

		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this surface is attached to. Updates z translation.")]
		public MonoBehaviour _surface;
		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		[SerializeField]
		[HideInInspector]
		public int Shape;

		#endregion

		public Vector2 Center => Position;

		public ISwitchable Switchable => Item;

		public override IEnumerable<Type> ValidParents => TriggerColliderAuthoring.ValidParentTypes
			.Concat(TriggerMeshAuthoring.ValidParentTypes)
			.Distinct();

		public bool IsCircle => Shape == TriggerShape.TriggerStar || Shape == TriggerShape.TriggerButton;

		protected override Trigger InstantiateItem(TriggerData data) => new Trigger(data);
		protected override TriggerData InstantiateData() => new TriggerData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Trigger, TriggerData, TriggerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Trigger, TriggerData, TriggerAuthoring>);


		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;

			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			var animComponent = GetComponentInChildren<TriggerAnimationAuthoring>();
			if (collComponent && animComponent) {
				dstManager.AddComponentData(entity, new TriggerAnimationData());
				dstManager.AddComponentData(entity, new TriggerMovementData());
				dstManager.AddComponentData(entity, new TriggerStaticData {
					AnimSpeed = animComponent.AnimSpeed,
					Radius = Radius,
					Shape = Data.Shape,
					TableScaleZ = table.GetScaleZ()
				});
			}

			// register
			var trigger = GetComponent<TriggerAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterTrigger(trigger, entity, ParentEntity, gameObject);
		}

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = Surface != null
				? new Vector3(Position.x, Position.y, Surface.Height(Position))
				: new Vector3(Position.x, Position.y, 0);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		public override IEnumerable<MonoBehaviour> SetData(TriggerData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			Rotation = data.Rotation;
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			UpdateTransforms();

			// geometry
			Radius = data.Radius;
			WireThickness = data.WireThickness;
			DragPoints = data.DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				mr.enabled = data.IsVisible;
			}

			// collider
			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			if (collComponent) {
				collComponent.enabled = data.IsEnabled;
				collComponent.HitHeight = data.HitHeight;
				updatedComponents.Add(collComponent);
			}

			// animation
			var animComponent = GetComponentInChildren<TriggerAnimationAuthoring>();
			if (animComponent) {
				animComponent.AnimSpeed = data.AnimSpeed;
				updatedComponents.Add(animComponent);
			}

			return updatedComponents;
		}

		public override TriggerData CopyDataTo(TriggerData data, string[] materialNames, string[] textureNames)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Rotation = Rotation;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// geometry
			data.Radius = Radius;
			data.WireThickness = WireThickness;
			data.DragPoints = DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				data.IsVisible = mr.enabled;
			}

			// collider
			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			if (collComponent) {
				data.IsEnabled = collComponent.gameObject.activeInHierarchy;
				data.HitHeight = collComponent.HitHeight;
			} else {
				data.IsEnabled = false;
			}

			// animation
			var animComponent = GetComponentInChildren<TriggerAnimationAuthoring>();
			if (animComponent) {
				animComponent.AnimSpeed = data.AnimSpeed;
			}

			return data;
		}

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Surface.Height(Position))
			: new Vector3(Position.x, Position.y, 0);

		public override void SetEditorPosition(Vector3 pos)
		{
			var newPos = (Vector2)((float3)pos).xy;
			if (DragPoints.Length > 0) {
				var diff = newPos - Position;
				foreach (var pt in DragPoints) {
					pt.Center += new Vertex3D(diff.x, diff.y, 0f);
				}
			}
			RebuildMeshes();
			Position = ((float3)pos).xy;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = rot.x;

		#endregion
	}
}
