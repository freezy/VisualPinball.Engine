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
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Trigger")]
	public class TriggerAuthoring : ItemMainRenderableAuthoring<TriggerData>,
		ITriggerAuthoring, IDragPointsAuthoring, IOnSurfaceAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position on the playfield.")]
		public Vector2 Position;

		[Tooltip("Rotation of the trigger.")]
		[Range(-180f, 180f)]
		public float Rotation;

		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this surface is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;
		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Trigger;
		public override string ItemName => "Trigger";

		public override IEnumerable<Type> ValidParents => TriggerColliderAuthoring.ValidParentTypes
			.Concat(TriggerMeshAuthoring.ValidParentTypes)
			.Distinct();

		public override TriggerData InstantiateData() => new TriggerData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<TriggerData, TriggerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<TriggerData, TriggerAuthoring>);

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(name)
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		#endregion

		#region Transformation

		public Vector2 Center => Position;

		public void OnSurfaceUpdated() => UpdateTransforms();
		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, PositionZ);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		#endregion

		#region Conversion

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			var animComponent = GetComponentInChildren<TriggerAnimationAuthoring>();
			var meshComponent = GetComponentInChildren<TriggerMeshAuthoring>();
			if (collComponent && animComponent && meshComponent) {
				dstManager.AddComponentData(entity, new TriggerAnimationData());
				dstManager.AddComponentData(entity, new TriggerMovementData());
				dstManager.AddComponentData(entity, new TriggerStaticData {
					AnimSpeed = animComponent.AnimSpeed,
					Radius = collComponent.HitCircleRadius,
					Shape = meshComponent.Shape,
					TableScaleZ = 1f
				});
			}

			// register
			transform.GetComponentInParent<Player>().RegisterTrigger(this, entity, ParentEntity);
		}

		public override IEnumerable<MonoBehaviour> SetData(TriggerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			Rotation = data.Rotation;

			// geometry
			DragPoints = data.DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				mr.enabled = data.IsVisible;
			}

			// mesh
			var meshComponent = GetComponent<TriggerMeshAuthoring>();
			if (meshComponent) {
				meshComponent.Shape = data.Shape;
				meshComponent.WireThickness = data.WireThickness;
				updatedComponents.Add(meshComponent);
			}

			// collider
			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			if (collComponent) {
				collComponent.enabled = data.IsEnabled;
				collComponent.HitHeight = data.HitHeight;
				collComponent.HitCircleRadius = data.Radius;
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

		public override IEnumerable<MonoBehaviour> SetReferencedData(TriggerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);

			// mesh
			var meshComponent = GetComponent<TriggerMeshAuthoring>();
			if (meshComponent) {
				meshComponent.CreateMesh(data, table, textureProvider, materialProvider);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override TriggerData CopyDataTo(TriggerData data, string[] materialNames, string[] textureNames)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Rotation = Rotation;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// geometry
			data.DragPoints = DragPoints;

			// visibility
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				data.IsVisible = mr.enabled;
			}

			// mesh
			var meshComponent = GetComponent<TriggerMeshAuthoring>();
			if (meshComponent) {
				data.WireThickness = meshComponent.WireThickness;
				data.Shape = meshComponent.Shape;
			}

			// collider
			var collComponent = GetComponentInChildren<TriggerColliderAuthoring>();
			if (collComponent) {
				data.IsEnabled = collComponent.gameObject.activeInHierarchy;
				data.HitHeight = collComponent.HitHeight;
				data.Radius = collComponent.HitCircleRadius;
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

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Surface.Height(Position))
			: new Vector3(Position.x, Position.y, 0); // todo? plus table height?

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
