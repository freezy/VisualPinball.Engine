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
		IGateData, ISwitchDeviceComponent, IRotatableAnimationComponent
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public float Rotation {
			get => transform.localEulerAngles.y > 180 ? transform.localEulerAngles.y - 360 : transform.localEulerAngles.y;
			set => transform.SetLocalYRotation(math.radians(value));
		}

		public float _length = 100f;

		public float Length
		{
			get {
				var scale = transform.localScale;
				if (math.abs(scale.x - scale.y) < Collider.Tolerance && math.abs(scale.x - scale.z) < Collider.Tolerance && math.abs(scale.y - scale.z) < Collider.Tolerance) {
					return scale.x * 100f;
				}
				return _length;
			}
			set {
				_length = value;
				var s = value / 100f;
				transform.localScale = new Vector3(s, s, s);
			}
		}

		public int _type;
		public string _meshName;

		#endregion

		#region IGateData

		public float PosX => Position.x;
		public float PosY => Position.y;
		public float Height => Position.z;

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


		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(GateData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector3(data.Height);
			Rotation = data.Rotation > 180f ? data.Rotation - 360f : data.Rotation;
			Length = data.Length;
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
			// surface
			ParentToSurface(data.Surface, data.Center, components);

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
			data.Center = Position.ToVertex2Dxy();
			data.Name = name;
			data.Rotation = Rotation;
			data.Height = Position.z;
			data.Length = Length;

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
			// collider data
			var collComp = GetComponent<GateColliderComponent>();
			var srcCollComp = go.GetComponent<GateColliderComponent>();
			if (collComp && srcCollComp) {
				collComp._angleMin = srcCollComp._angleMin;
				collComp._angleMax = srcCollComp._angleMax;
				collComp.Damping = srcCollComp.Damping;
				collComp.Elasticity = srcCollComp.Elasticity;
				collComp.Friction = srcCollComp.Friction;
				collComp.GravityFactor = srcCollComp.GravityFactor;
				collComp._twoWay = srcCollComp.TwoWay;
			}

			// gate bracket visibility
			var bracketComp = GetComponentInChildren<GateBracketComponent>(true);
			var srcBracketComp = go.GetComponentInChildren<GateBracketComponent>(true);
			if (bracketComp && srcBracketComp) {
				bracketComp.gameObject.SetActive(srcBracketComp.gameObject.activeInHierarchy);
			}
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
