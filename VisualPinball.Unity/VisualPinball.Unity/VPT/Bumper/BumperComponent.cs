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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[PackAs("Bumper")]
	[AddComponentMenu("Visual Pinball/Game Item/Bumper")]
	public class BumperComponent : MainRenderableComponent<BumperData>, ISwitchDeviceComponent, ICoilDeviceComponent, IPackable
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		[Range(20f, 250f)]
		[Tooltip("Radius of the bumper. Updates xy scaling. 50 = Original size.")]
		public float Radius = 45f;

		public float HeightScale
		{
			get => transform.localScale.y * DataMeshScale;
			set => transform.localScale = new Vector3(transform.localScale.x, value / DataMeshScale, transform.localScale.z);

		}

		public float Orientation {
			get => transform.localEulerAngles.y > 180 ? transform.localEulerAngles.y - 360 : transform.localEulerAngles.y;
			set => transform.SetLocalYRotation(math.radians(value));
		}

		[Tooltip("Should the bumper coil always activate when touched by a ball? Disable to give game logic engine full control")]
		public bool IsHardwired = true;

		private IEnumerable<GamelogicEngineCoil> _availableDeviceItems;

		#endregion

		#region Packaging

		public byte[] Pack() => BumperPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => Array.Empty<byte>();

		public void Unpack(byte[] bytes) => BumperPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Bumper;
		public override string ItemName => "Bumper";
		public override bool HasProceduralMesh => false;

		public override BumperData InstantiateData() => new();

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<BumperData, BumperComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<BumperData, BumperComponent>);

		public const float DataMeshScale = 100f;

		public const string SocketSwitchItem = "socket_switch";

		#endregion

		#region Runtime

		public BumperApi BumperApi { get; private set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			BumperApi = new BumperApi(gameObject, Player, physicsEngine);

			Player.Register(BumperApi, this);
			if (GetComponentInChildren<BumperColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		private void Start()
		{
			if (IsHardwired) {
				WireMapping wireMapping = new() {
					Description = $"Hardwired bumper '{name}'",
					Source = SwitchSource.Playfield,
					SourceDevice = this,
					SourceDeviceItem = AvailableSwitches.FirstOrDefault().Id,
					DestinationDevice = this,
					DestinationDeviceItem = AvailableCoils.FirstOrDefault().Id,
					IsDynamic = false,
				};
				WireDestConfig wireDestConfig = new(wireMapping.WithId());
				BumperApi.AddWireDest(wireDestConfig);
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

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			 // this is for when the radius is changed, in this case we syn x/z scale
			 transform.localScale = new Vector3(Radius * 2f, HeightScale, Radius * 2f) / DataMeshScale;
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(BumperData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector3(0);
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
			// surface
			ParentToSurface(data.Surface, data.Center, components);

			UpdateTransforms();

			// children visibility
			SetVisibilityByComponent<BumperSkirtAnimationComponent>(data.IsSocketVisible);
			SetVisibilityByComponent<BumperBaseComponent>(data.IsBaseVisible);
			SetVisibilityByComponent<BumperCapComponent>(data.IsCapVisible);
			SetVisibilityByComponent<BumperRingAnimationComponent>(data.IsRingVisible);

			return Array.Empty<MonoBehaviour>();
		}

		public override BumperData CopyDataTo(BumperData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = new Vertex2D(Position.x, Position.y);
			data.Radius = Radius;
			data.HeightScale = HeightScale;
			data.Orientation = Orientation;

			// children visibility
			data.IsBaseVisible = CopyMaterialName<BumperBaseComponent>(data, materialNames, textureNames);
			data.IsCapVisible = CopyMaterialName<BumperCapComponent>(data, materialNames, textureNames);
			data.IsRingVisible = CopyMaterialName<BumperRingAnimationComponent>(data, materialNames, textureNames);
			data.IsSocketVisible = CopyMaterialName<BumperSkirtAnimationComponent>(data, materialNames, textureNames);

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

		private bool CopyMaterialName<TComponent>(BumperData data, string[] materialNames, string[] textureNames) where TComponent : MonoBehaviour
		{
			var skirtComp = GetComponentInChildren<TComponent>();
			if (skirtComp) {
				var mf = skirtComp.GetComponentInChildren<MeshFilter>();
				var mr = mf.gameObject.GetComponent<MeshRenderer>();
				CopyMaterialName(mr, materialNames, textureNames, ref data.SocketMaterial);
				return mf.gameObject.activeInHierarchy;
			}
			return false;
		}

		public override void CopyFromObject(GameObject go)
		{
			// main component
			var srcMainComp = go.GetComponent<BumperComponent>();
			if (srcMainComp) {
				Radius = srcMainComp.Radius;
				HeightScale = srcMainComp.HeightScale;
				Orientation = srcMainComp.Orientation;
				IsHardwired = srcMainComp.IsHardwired;
			}

			// collider comp
			var srcCollComp = go.GetComponent<BumperColliderComponent>();
			var collComp = GetComponent<BumperColliderComponent>();
			if (srcCollComp && collComp) {
				collComp.enabled = srcCollComp.enabled;
				collComp.Threshold = srcCollComp.Threshold;
				collComp.Force = srcCollComp.Force;
				collComp.Scatter = srcCollComp.Scatter;
				collComp.HitEvent = srcCollComp.HitEvent;
			}

			// ring animation
			var ringAnimComp = GetComponentInChildren<BumperRingAnimationComponent>();
			var srcRingAnimComp = go.GetComponentInChildren<BumperRingAnimationComponent>();
			if (ringAnimComp && srcRingAnimComp) {
				ringAnimComp.RingSpeed = srcRingAnimComp.RingSpeed;
				ringAnimComp.RingDropOffset = srcRingAnimComp.RingDropOffset;
			}

			// skirt animation
			var skirtAnimComp = GetComponentInChildren<BumperSkirtAnimationComponent>();
			var srcSkirtAnimComp = go.GetComponentInChildren<BumperSkirtAnimationComponent>();
			if (ringAnimComp && srcSkirtAnimComp) {
				skirtAnimComp.duration = srcSkirtAnimComp.duration;
			}
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
					Center = Position,
					Duration = skirtAnimComponent.duration,
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
	}
}
