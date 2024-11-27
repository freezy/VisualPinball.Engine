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
		ITriggerComponent, IOnSurfaceComponent
	{
		#region Data

		private Vector3 _position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public Vector2 Position {
			get => _position.XY();
			set => _position = new Vector3(value.x, value.y, _position.z);
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
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this surface is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;
		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }

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

		public Vector2 Center => Position;

		public void OnSurfaceUpdated() => UpdateTransforms();
		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// position
			t.localPosition = Physics.TranslateToWorld(Position.x, Position.y, PositionZ);

			// scale
			t.localScale = new Vector3(Scale, Scale, Scale);

			// rotation
			t.localEulerAngles = new Vector3(0, Rotation, 0);
		}

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
			DragPoints = data.DragPoints;

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
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);

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
			data.Center = Position.ToVertex2D();
			data.Rotation = Rotation;
			data.Surface = Surface != null ? Surface.name : string.Empty;

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
			if (triggerComponent != null) {
				Position = triggerComponent.Position;
				Scale = triggerComponent.Scale;
				Rotation = triggerComponent.Rotation;
				Surface = triggerComponent.Surface;
				_dragPoints = triggerComponent._dragPoints.Select(dp => dp.Clone()).ToArray();

			} else {
				var pos = go.transform.localPosition.TranslateToVpx();
				MoveDragPointsTo(_dragPoints, pos);
				Position = pos;
				Rotation = go.transform.localEulerAngles.z;
			}

			UpdateTransforms();
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
						TableScaleZ = 1f
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
					TableScaleZ = 1f
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

		public override ItemDataTransformType EditorRotationType{
			get {
				var meshComp = GetComponent<TriggerMeshComponent>();
				return !meshComp || !meshComp.IsCircle ? ItemDataTransformType.None : ItemDataTransformType.OneD;
			}
		}
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = ClampDegrees(rot.x);

		#endregion
	}
}
