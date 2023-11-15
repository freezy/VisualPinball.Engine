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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Bumper")]
	public class BumperComponent : MainRenderableComponent<BumperData>,
		ISwitchDeviceComponent, ICoilDeviceComponent, IOnSurfaceComponent
	{
		#region Data

		[Tooltip("Position of the bumper on the playfield.")]
		public Vector2 Position;

		[Range(20f, 250f)]
		[Tooltip("Radius of the bumper. Updates xy scaling. 50 = Original size.")]
		public float Radius = 45f;

		[Range(50f, 300f)]
		[Tooltip("Height of the bumper. Updates z scaling. 100 = Original size.")]
		public float HeightScale = 45f;

		[Range(-180f, 180f)]
		[Tooltip("Orientation angle. Updates z rotation.")]
		public float Orientation;

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }

		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this bumper is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;
		private IEnumerable<GamelogicEngineCoil> _availableDeviceItems;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Bumper;
		public override string ItemName => "Bumper";
		public override bool HasProceduralMesh => false;

		public override BumperData InstantiateData() => new BumperData();

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<BumperData, BumperComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<BumperData, BumperComponent>);
		private const string SkirtMeshName = "bumper.skirt";
		private const string BaseMeshName = "bumper.base";
		private const string CapMeshName = "bumper.cap";
		private const string RingMeshName = "bumper.ring";

		public const float DataMeshScale = 100f;

		public const string SocketSwitchItem = "socket_switch";

		#endregion

		#region Runtime

		public BumperApi BumperApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			BumperApi = new BumperApi(gameObject, player, physicsEngine);

			player.Register(BumperApi, this);
			if (GetComponentInChildren<BumperColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SocketSwitchItem) {
				Description = "Socket Switch",
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils =>  new[] {
			new GamelogicEngineCoil(name) {
				Description = "Ring Coil"
			}
		};

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => UpdateTransforms();

		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// position
			t.localPosition = Physics.TranslateToWorld(Position.x, Position.y, PositionZ);

			// scale
			t.localScale = new Vector3(Radius * 2f, HeightScale, Radius * 2f) / DataMeshScale;

			// rotation
			t.localEulerAngles = new Vector3(0, Orientation, 0);
		}

		public float4x4 TransformationWithinPlayfield {
			get {
				var transMatrix = float4x4.Translate(new float3(Position.x, Position.y, PositionZ));
				var scaleMatrix = float4x4.Scale(new float3(Radius * 2f, Radius * 2f, HeightScale) / DataMeshScale);
				var rotMatrix = float4x4.RotateZ(math.radians(Orientation));
				return math.mul(transMatrix, math.mul(rotMatrix, scaleMatrix));
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(BumperData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityFloat2();
			Radius = data.Radius;
			HeightScale = data.HeightScale;
			Orientation = data.Orientation;

			// collider
			var collComponent = GetComponentInChildren<BumperColliderComponent>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;
				collComponent.Threshold = data.Threshold;
				collComponent.Force = data.Force;
				collComponent.Scatter = data.Scatter;
				collComponent.HitEvent = data.HitEvent;
			}

			// ring animation
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationComponent>();
			if (ringAnimComponent) {
				ringAnimComponent.RingSpeed = data.RingSpeed;
				ringAnimComponent.RingDropOffset = data.RingDropOffset;
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(BumperData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);
			UpdateTransforms();

			// children visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				if (mf.sharedMesh) {
					var mr = mf.GetComponent<MeshRenderer>();
					switch (mf.sharedMesh.name) {
						case SkirtMeshName:
							mf.gameObject.SetActive(data.IsSocketVisible);
							if (!string.IsNullOrEmpty(data.SocketMaterial)) {
								mr.sharedMaterial = materialProvider.MergeMaterials(data.SocketMaterial, mr.sharedMaterial);
							}
							break;
						case BaseMeshName:
							mf.gameObject.SetActive(data.IsBaseVisible);
							if (!string.IsNullOrEmpty(data.BaseMaterial)) {
								mr.sharedMaterial = materialProvider.MergeMaterials(data.BaseMaterial, mr.sharedMaterial);
							}
							break;
						case CapMeshName:
							mf.gameObject.SetActive(data.IsCapVisible);
							if (!string.IsNullOrEmpty(data.CapMaterial)) {
								mr.sharedMaterial = materialProvider.MergeMaterials(data.CapMaterial, mr.sharedMaterial);
							}
							break;
						case RingMeshName:
							mf.gameObject.SetActive(data.IsRingVisible);
							if (!string.IsNullOrEmpty(data.RingMaterial)) {
								mr.sharedMaterial = materialProvider.MergeMaterials(data.RingMaterial, mr.sharedMaterial);
							}
							break;
					}
				}
			}

			return Array.Empty<MonoBehaviour>();
		}


		public override BumperData CopyDataTo(BumperData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Radius = Radius;
			data.HeightScale = HeightScale;
			data.Orientation = Orientation;

			// surface
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// children visibility
			data.IsBaseVisible = false;
			data.IsCapVisible = false;
			data.IsRingVisible = false;
			data.IsSocketVisible = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
				if (mf.sharedMesh) {
					var mr = mf.gameObject.GetComponent<MeshRenderer>();
					switch (mf.sharedMesh.name) {
						case SkirtMeshName:
							data.IsSocketVisible = mf.gameObject.activeInHierarchy;
							CopyMaterialName(mr, materialNames, textureNames, ref data.SocketMaterial);
							break;
						case BaseMeshName:
							data.IsBaseVisible = mf.gameObject.activeInHierarchy;
							CopyMaterialName(mr, materialNames, textureNames, ref data.BaseMaterial);
							break;
						case CapMeshName:
							data.IsCapVisible = mf.gameObject.activeInHierarchy;
							CopyMaterialName(mr, materialNames, textureNames, ref data.CapMaterial);
							break;
						case RingMeshName:
							data.IsRingVisible = mf.gameObject.activeInHierarchy;
							CopyMaterialName(mr, materialNames, textureNames, ref data.RingMaterial);
							break;
					}
				}
			}

			// collider
			var collComponent = GetComponentInChildren<BumperColliderComponent>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;
				data.Threshold = collComponent.Threshold;
				data.Force = collComponent.Force;
				data.Scatter = collComponent.Scatter;
				data.HitEvent = collComponent.HitEvent;
			} else {
				data.IsCollidable = false;
			}

			// ring animation
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationComponent>();
			if (ringAnimComponent) {
				data.RingSpeed = ringAnimComponent.RingSpeed;
				data.RingDropOffset = ringAnimComponent.RingDropOffset;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var bumperComponent = go.GetComponent<BumperComponent>();
			if (bumperComponent != null) {
				Position = bumperComponent.Position;
				Radius = bumperComponent.Radius;
				HeightScale = bumperComponent.HeightScale;
				Orientation = bumperComponent.Orientation;
				Surface = bumperComponent.Surface;

			} else {
				var scale = go.transform.localScale;
				Position = go.transform.localPosition.TranslateToVpx();
				Orientation = go.transform.localEulerAngles.z;
				Radius = scale.x / 2 * DataMeshScale;
				HeightScale = scale.z * DataMeshScale;
			}

			UpdateTransforms();
		}

		#endregion

		#region State

		internal BumperState CreateState()
		{
			// physics collision data
			var collComponent = GetComponentInChildren<BumperColliderComponent>();
			var staticData = collComponent
				? new BumperStaticState {
					Force = collComponent.Force,
					HitEvent = collComponent.HitEvent,
					Threshold = collComponent.Threshold
				} : default;

			// skirt animation data
			var skirtAnimComponent = GetComponentInChildren<BumperSkirtAnimationComponent>();
			var skirtAnimation = skirtAnimComponent
				? new BumperSkirtAnimationState {
					BallPosition = default,
					AnimationCounter = 0f,
					DoAnimate = false,
					DoUpdate = false,
					EnableAnimation = true,
					Rotation = new float2(0, 0),
					Center = Position
				} : default;

			// ring animation data
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationComponent>();
			var ringAnimation = ringAnimComponent
				? new BumperRingAnimationState {

					// dynamic
					IsHit = false,
					Offset = 0,
					AnimateDown = false,
					DoAnimate = false,

					// static
					DropOffset = ringAnimComponent.RingDropOffset,
					HeightScale = HeightScale,
					Speed = ringAnimComponent.RingSpeed,
				} : default;

			return new BumperState(
				skirtAnimComponent ? skirtAnimComponent.gameObject.GetInstanceID() : 0,
				ringAnimComponent ? ringAnimComponent.gameObject.GetInstanceID() : 0,
				staticData,
				ringAnimation,
				skirtAnimation
			);
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Surface.Height(Position))
			: new Vector3(Position.x, Position.y, 0);
		public override void SetEditorPosition(Vector3 pos) => Position = ((float3)pos).xy;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Orientation, 0, 0);
		public override void SetEditorRotation(Vector3 rot) => Orientation = ClampDegrees(rot.x);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Radius * 2f, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Radius = scale.x / 2f;

		#endregion
	}
}
