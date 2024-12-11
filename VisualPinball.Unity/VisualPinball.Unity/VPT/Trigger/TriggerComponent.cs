// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Unity.Collections;
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
	public class TriggerComponent : MainRenderableComponent<TriggerData>,
		ITriggerComponent
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public float _scale = 1f;
		public float Scale
		{
			get {
				var scale = transform.localScale;
				if (math.abs(scale.x - scale.y) < Collider.Tolerance && math.abs(scale.x - scale.z) < Collider.Tolerance && math.abs(scale.y - scale.z) < Collider.Tolerance) {
					return scale.x;
				}
				return _scale;
			}
			set {
				_scale = value;
				transform.localScale = new Vector3(value, value, value);
			}
		}

		public float Rotation {
			get => transform.localEulerAngles.y > 180 ? transform.localEulerAngles.y - 360 : transform.localEulerAngles.y;
			set => transform.SetLocalYRotation(math.radians(value));
		}

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Trigger;
		public override string ItemName => "Trigger";

		public override TriggerData InstantiateData() => new TriggerData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<TriggerData, TriggerComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<TriggerData, TriggerComponent>);

		public const string SwitchItem = "trigger_switch";

		#endregion

		#region Runtime

		public TriggerApi TriggerApi { get; set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			TriggerApi = new TriggerApi(gameObject, Player, physicsEngine);

			Player.Register(TriggerApi, this);
			if (GetComponentInChildren<TriggerColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}


		private void Start()
		{
			_playfieldToWorld = Player.PlayfieldToWorldMatrix;
		}

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem)
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		public Vector2 Center => Position; // todo remove?

		public void OnSurfaceUpdated() => UpdateTransforms();
		public float4x4 TransformationMatrix => transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(TriggerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			Rotation = data.Rotation;

			// geometry
			DragPoints = data.DragPoints.Select(dp => dp.Translate((-transform.localPosition).TranslateToVpx().ToVertex3D())).ToArray();

			// mesh
			var meshComponent = GetComponent<TriggerMeshComponent>();
			if (meshComponent) {
				meshComponent.Shape = data.Shape;
				meshComponent.WireThickness = data.WireThickness;
				updatedComponents.Add(meshComponent);
			}

			// collider
			var collComponent = GetComponent<TriggerColliderComponent>();
			if (collComponent) {
				collComponent.enabled = data.IsEnabled;
				collComponent.HitHeight = data.HitHeight;
				collComponent.HitCircleRadius = data.Radius;
				updatedComponents.Add(collComponent);
			}

			// animation
			var animComponent = GetComponent<TriggerAnimationComponent>();
			if (animComponent) {
				animComponent.AnimSpeed = data.AnimSpeed;
				updatedComponents.Add(animComponent);
			}
			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TriggerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// surface
			ParentToSurface(data.Surface, data.Center, components);

			// mesh
			var meshComponent = GetComponent<TriggerMeshComponent>();
			if (meshComponent) {
				meshComponent.CreateMesh(data, table, textureProvider, materialProvider);
				meshComponent.enabled = data.IsVisible;
				SetEnabled<Renderer>(data.IsVisible && meshComponent.Shape != TriggerShape.TriggerNone);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override TriggerData CopyDataTo(TriggerData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = new Vertex2D(Position.x, Position.y);
			data.Rotation = Rotation;

			// geometry
			data.DragPoints = DragPoints;

			// visibility
			data.IsVisible = GetEnabled<Renderer>();

			// mesh
			var meshComponent = GetComponent<TriggerMeshComponent>();
			if (meshComponent) {
				data.WireThickness = meshComponent.WireThickness;
				data.Shape = meshComponent.Shape;
			}

			// collider
			var collComponent = GetComponent<TriggerColliderComponent>();
			if (collComponent) {
				data.IsEnabled = collComponent.gameObject.activeInHierarchy;
				data.HitHeight = collComponent.HitHeight;
				data.Radius = collComponent.HitCircleRadius;
			} else {
				data.IsEnabled = false;
			}

			// animation
			var animComponent = GetComponent<TriggerAnimationComponent>();
			if (animComponent) {
				animComponent.AnimSpeed = data.AnimSpeed;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var triggerComponent = go.GetComponent<TriggerComponent>();
			if (triggerComponent) {
				_dragPoints = triggerComponent._dragPoints.Select(dp => dp.Clone()).ToArray();
			}
			RebuildMeshes();
		}

		#endregion

		#region State

		internal TriggerState CreateState()
		{
			var collComponent = GetComponentInChildren<TriggerColliderComponent>();
			var animComponent = GetComponentInChildren<TriggerAnimationComponent>();
			var meshComponent = GetComponentInChildren<TriggerMeshComponent>();

			if (collComponent.ForFlipper == null) {
				return new TriggerState(
					animComponent ? animComponent.gameObject.GetInstanceID() : 0,
					new TriggerStaticState {
						AnimSpeed = animComponent ? animComponent.AnimSpeed : 0,
						Radius = collComponent.HitCircleRadius,
						Shape = meshComponent ? meshComponent.Shape : 0,
						TableScaleZ = 1f,
						InitialPosition = transform.localPosition
					},
					new TriggerMovementState(),
					new TriggerAnimationState()
				);
			}

			return new TriggerState(
				new TriggerStaticState {
					AnimSpeed = 0,
					Radius = collComponent.HitCircleRadius,
					Shape = TriggerShape.TriggerNone,
					TableScaleZ = 1f,
					InitialPosition = transform.position
				},
				new FlipperCorrectionState(
					true,
					collComponent.ForFlipper.gameObject.GetInstanceID(),
					collComponent.ForFlipper.FlipperApi.ColliderId, // todo fixme this is not yet set
					collComponent.TimeThresholdMs,
					collComponent.FlipperPolarities,
					collComponent.FlipperVelocities,
					Allocator.Persistent
				)
			);
		}

		#endregion
	}
}
