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
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Kicker")]
	public class KickerComponent : MainRenderableComponent<KickerData>,
		ICoilDeviceComponent, ITriggerComponent, IBallCreationPosition,
		IRotatableComponent, ISerializationCallbackReceiver
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public float Radius {
			get {
				var scale = transform.localScale;
				if (math.abs(scale.x - scale.y) < Collider.Tolerance && math.abs(scale.x - scale.z) < Collider.Tolerance && math.abs(scale.y - scale.z) < Collider.Tolerance) {
					return scale.x * 25f;
				}
				return _radius;
			}
			set {
				_radius = value;
				var s = value / 25f;
				transform.localScale = new Vector3(s, s, s);
			}
		}

		private float _radius = 25f;

		[Tooltip("R-Rotation of the kicker")]
		public float Orientation;

		public List<KickerCoil> Coils = new() {
			new KickerCoil { Name = "Default Coil" }
		};

		[HideInInspector] public int KickerType;
		[HideInInspector] public string MeshName;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Kicker;
		public override string ItemName => "Kicker";

		public override KickerData InstantiateData() => new KickerData();

		public override bool HasProceduralMesh => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<KickerData, KickerComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<KickerData, KickerComponent>);

		public Vector2 Center => Position;

		public const string SwitchItem = "kicker_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem),
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => Coils.Select(c => new GamelogicEngineCoil(c.Id) { Description = c.Name });

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Transformation

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// todo move this to import
			if (KickerType == Engine.VPT.KickerType.KickerCup) {
				t.localPosition += Physics.TranslateToWorld(0, 0, -0.18f * Radius);
			}

			// scale
			t.localScale = KickerType == Engine.VPT.KickerType.KickerInvisible
				? Vector3.one
				: Physics.ScaleToWorld(Radius, Radius, Radius);

			switch (KickerType) {
				// rotation
				case Engine.VPT.KickerType.KickerCup:
					t.localEulerAngles = Physics.RotateToWorld(0, 0, Orientation);
					break;
				case Engine.VPT.KickerType.KickerWilliams:
					t.localEulerAngles = Physics.RotateToWorld(0, 0, Orientation + 90f);
					break;
				case Engine.VPT.KickerType.KickerInvisible:
					t.localRotation = Quaternion.identity;
					break;
				default:
					t.localEulerAngles = Physics.RotateToWorld(0, 0, Orientation);
					break;
			}
		}

		private float _originalRotationZ;
		private float _originalKickerAngle;

		public float RotateZ {
			set {
				Orientation = _originalRotationZ + value;
				KickerApi.KickerCoil.Coil.Angle = _originalKickerAngle + value;
			}
		}

		public float2 RotatedPosition {
			get => new(Position.x, Position.y);
			set {
				Position = new Vector2(value.x, value.y);
				UpdateTransforms();
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(KickerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			Orientation = data.Orientation > 180f ? data.Orientation - 360f : data.Orientation;
			Radius = data.Radius;
			KickerType = data.KickerType;

			#if UNITY_EDITOR
			var mf = GetComponent<MeshFilter>();
			if (mf) {
				MeshName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mf.sharedMesh));
			}
			#endif

			// collider data
			var colliderComponent = gameObject.GetComponent<KickerColliderComponent>();
			if (colliderComponent) {
				colliderComponent.enabled = data.IsEnabled;

				colliderComponent.Scatter = data.Scatter;
				colliderComponent.HitAccuracy = data.HitAccuracy;
				colliderComponent.HitHeight = data.HitHeight;
				colliderComponent.FallThrough = data.FallThrough;
				colliderComponent.LegacyMode = data.LegacyMode;

				updatedComponents.Add(colliderComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(KickerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			return Array.Empty<MonoBehaviour>();
		}

		public override KickerData CopyDataTo(KickerData data, string[] materialNames, string[] textureNames,
			bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = new Vertex2D(Position.x, Position.y);
			data.Orientation = Orientation;
			data.Radius = Radius;

			data.KickerType = KickerType;

			// todo visibility is set by the type

			var colliderComponent = gameObject.GetComponent<KickerColliderComponent>();
			if (colliderComponent) {
				data.IsEnabled = colliderComponent.enabled;
				data.Scatter = colliderComponent.Scatter;
				data.HitAccuracy = colliderComponent.HitAccuracy;
				data.HitHeight = colliderComponent.HitHeight;
				data.FallThrough = colliderComponent.FallThrough;
				data.LegacyMode = colliderComponent.LegacyMode;

			} else {
				data.IsEnabled = false;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var kickerComponent = go.GetComponent<KickerComponent>();
			if (kickerComponent != null) {
				Position = kickerComponent.Position;
				Radius = kickerComponent.Radius;
				Orientation = kickerComponent.Orientation;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
				Radius = go.transform.localScale.x;
				Orientation = go.transform.localEulerAngles.z;
			}

			UpdateTransforms();
		}

		#endregion

		#region State

		internal KickerState CreateState()
		{
			// collision
			var colliderComponent = GetComponent<KickerColliderComponent>();
			var staticData = colliderComponent
				? new KickerStaticState {
					Center = new float2(Position.x, Position.y),
					FallIn = colliderComponent.FallIn,
					FallThrough = colliderComponent.FallThrough,
					HitAccuracy = colliderComponent.HitAccuracy,
					Scatter = colliderComponent.Scatter,
					LegacyMode = colliderComponent.LegacyMode,
					ZLow = Position.z
				} : default;

			var meshData = colliderComponent.LegacyMode
				? new ColliderMeshData(Array.Empty<Vertex3DNoTex2>(), 0, float3.zero, Allocator.Persistent)
				: new ColliderMeshData(KickerHitMesh.Vertices, Radius, Position, Allocator.Persistent);

			return new KickerState(
				staticData,
				new KickerCollisionState(),
				meshData
			);
		}

		#endregion

		#region Serialization

		public void OnBeforeSerialize()
		{
			#if UNITY_EDITOR

			// don't generate ids for prefabs, otherwise they'll show up in the instances.
			if (PrefabUtility.GetPrefabInstanceStatus(this) != PrefabInstanceStatus.Connected) {
				return;
			}
			var coilIds = new HashSet<string>();
			foreach (var coil in Coils) {
				if (!coil.HasId || coilIds.Contains(coil.Id)) {
					coil.GenerateId();
				}
				coilIds.Add(coil.Id);
			}
			#endif
		}

		public void OnAfterDeserialize()
		{
		}

		#endregion

		#region Runtime

		public KickerApi KickerApi { get; private set; }

		private void Awake()
		{
			_originalRotationZ = Orientation;

			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			KickerApi = new KickerApi(gameObject, player, physicsEngine);

			player.Register(KickerApi, this);
			if (GetComponent<KickerColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}
		}

		private void Start()
		{
			if (KickerApi?.KickerCoil != null) {
				_originalKickerAngle = KickerApi.KickerCoil.Coil.Angle;
			}
		}

		#endregion

		#region IBallCreationPosition

		public Vertex3D GetBallCreationPosition() => new Vertex3D(Position.x, Position.y, 0);

		public Vertex3D GetBallCreationVelocity() => new Vertex3D(0.1f, 0, 0);

		#endregion
	}

	[Serializable]
	public class KickerCoil
	{
		public string Name;
		[SerializeField, HideInInspector]
		public string Id;
		public float Speed = 3f;
		public float Angle = 90f;
		public float Inclination;

		internal bool HasId => !string.IsNullOrEmpty(Id);
		internal void GenerateId() => Id = $"coil_{Guid.NewGuid().ToString().Substring(0, 8)}";
	}
}
