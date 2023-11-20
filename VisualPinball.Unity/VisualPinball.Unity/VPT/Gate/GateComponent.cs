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
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Gate")]
	public class GateComponent : MainRenderableComponent<GateData>,
		IGateData, ISwitchDeviceComponent, IOnSurfaceComponent, IRotatableAnimationComponent
	{
		#region Data

		[Tooltip("Position of the gate on the playfield.")]
		public Vector3 Position;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the gate on the playfield (z-axis rotation)")]
		public float _rotation;

		[Range(10f, 250f)]
		[Tooltip("How much the gate is scaled, in percent.")]
		public float _length = 100f;

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this flipper is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		public int _type;
		public string _meshName;

		#endregion

		#region IGateData

		public float PosX => Position.x;
		public float PosY => Position.y;
		public float Height => Position.z;

		public float Rotation => _rotation;
		public float Length => _length;

		public bool ShowBracket { get {
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						return mf.gameObject.activeInHierarchy;
				}
			}
			return false;
		}}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Gate;
		public override string ItemName => "Gate";

		public override bool HasProceduralMesh => false;

		public override GateData InstantiateData() => new GateData();

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<GateData, GateComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<GateData, GateComponent>);

		public const string BracketObjectName = "Bracket";
		public const string WireObjectName = "Wire";

		public const string MainSwitchItem = "gate_switch";

		#endregion

		#region Runtime

		[NonSerialized]
		private IRotatableAnimationComponent[] _animatedComponents;

		public GateApi GateApi { get; private set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			GateApi = new GateApi(gameObject, Player, physicsEngine);

			Player.Register(GateApi, this);
			if (GetComponent<GateColliderComponent>()) {
				RegisterPhysics(physicsEngine);
			}

			_animatedComponents = GetComponentsInChildren<GateWireAnimationComponent>()
				.Select(gwa => gwa as IRotatableAnimationComponent)
				.ToArray();
		}

		private void Start()
		{
			_playfieldToWorld = Player.PlayfieldToWorldMatrix;
		}

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(MainSwitchItem)  {
				IsPulseSwitch = true
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		public void OnSurfaceUpdated() => UpdateTransforms();

		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// position
			t.localPosition = Physics.TranslateToWorld(Position.x, Position.y, Position.z + PositionZ);

			// scale
			t.localScale = new float3(Length * 0.01f);

			// rotation
			t.localRotation = quaternion.RotateY(math.radians(Rotation));
		}

		public float4x4 TransformationWithinPlayfield => transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);


		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(GateData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector3(data.Height);
			_rotation = data.Rotation > 180f ? data.Rotation - 360f : data.Rotation;
			_length = data.Length;
			_type = data.GateType;

			// collider data
			var colliderComponent = gameObject.GetComponent<GateColliderComponent>();
			if (colliderComponent) {
				colliderComponent._angleMin = math.degrees(data.AngleMin);
				colliderComponent._angleMax = math.degrees(data.AngleMax);
				if (colliderComponent._angleMin > 180f) {
					colliderComponent._angleMin -= 360f;
				}
				if (colliderComponent._angleMax > 180f) {
					colliderComponent._angleMax -= 360f;
				}
				colliderComponent.Damping = data.Damping;
				colliderComponent.Elasticity = data.Elasticity;
				colliderComponent.Friction = data.Friction;
				colliderComponent.GravityFactor = data.GravityFactor;
				colliderComponent._twoWay = data.TwoWay;

				updatedComponents.Add(colliderComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(GateData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						mf.gameObject.SetActive(data.IsVisible && data.ShowBracket);
						break;
					case WireObjectName:
						#if UNITY_EDITOR
						_meshName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mf.sharedMesh));
						#endif
						mf.gameObject.SetActive(data.IsVisible);
						break;
					default:
						mf.gameObject.SetActive(data.IsVisible);
						break;
				}
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override GateData CopyDataTo(GateData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2Dxy();
			data.Rotation = Rotation;
			data.Height = Position.z;
			data.Length = Length;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			data.GateType = _type;

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						data.ShowBracket = mf.gameObject.activeInHierarchy;
						break;
					case WireObjectName:
						data.IsVisible = mf.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision data
			var colliderComponent = gameObject.GetComponent<GateColliderComponent>();
			if (colliderComponent) {
				data.IsCollidable = colliderComponent.enabled;

				data.AngleMin = math.radians(colliderComponent._angleMin);
				data.AngleMax = math.radians(colliderComponent._angleMax);
				data.Damping = colliderComponent.Damping;
				data.Elasticity = colliderComponent.Elasticity;
				data.Friction = colliderComponent.Friction;
				data.GravityFactor = colliderComponent.GravityFactor;
				data.TwoWay = colliderComponent._twoWay;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var gateComponent = go.GetComponent<GateComponent>();
			if (gateComponent != null) {
				Position = gateComponent.Position;
				_rotation = gateComponent._rotation;
				_length = gateComponent._length;
				Surface = gateComponent.Surface;

			} else {

				Position = go.transform.localPosition.TranslateToVpx();
				_rotation = go.transform.localEulerAngles.z;
			}

			UpdateTransforms();
		}

		#endregion

		#region State

		internal GateState CreateState()
		{
			// collision
			var collComponent = GetComponent<GateColliderComponent>();
			var staticData = collComponent
				? new GateStaticState {
					AngleMin = math.radians(collComponent._angleMin),
					AngleMax = math.radians(collComponent._angleMax),
					Height = Position.z,
					Damping = math.pow(math.clamp(collComponent.Damping, 0, 1), (float)PhysicsConstants.PhysFactor),
					GravityFactor = collComponent.GravityFactor,
					TwoWay = collComponent.TwoWay,
				} : default;
			Debug.Log($"Damping = {staticData.Damping}");

			var wireComponent = GetComponentInChildren<GateWireAnimationComponent>();
			var movementData = collComponent && wireComponent
				? new GateMovementState {
					Angle = math.radians(collComponent._angleMin),
					AngleSpeed = 0,
					ForcedMove = false,
					IsOpen = false,
					HitDirection = false
				} : default;

			return new GateState(
				wireComponent ? wireComponent.gameObject.GetInstanceID() : 0,
				staticData,
				movementData
			);
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos) => Position = Surface != null 
			? pos - new Vector3(0, 0, Surface.Height(Position))
			: pos;
		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Position.z + Surface.Height(Position))
			: new Vector3(Position.x, Position.y, Position.z);


		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => _rotation = ClampDegrees(rot.x);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => _length = scale.x;

		#endregion

		#region IRotatableAnimationComponent

		public void OnRotationUpdated(float angleRad)
		{
			foreach (var animatedComponent in _animatedComponents) {
				animatedComponent.OnRotationUpdated(angleRad);
			}
		}

		#endregion
	}
}
