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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Spinner")]
	public class SpinnerComponent : MainRenderableComponent<SpinnerData>,
		ISwitchDeviceComponent, IRotatableAnimationComponent
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

		public float Length
		{
			get {
				var scale = transform.localScale;
				if (math.abs(scale.x - scale.y) < Collider.Tolerance && math.abs(scale.x - scale.z) < Collider.Tolerance && math.abs(scale.y - scale.z) < Collider.Tolerance) {
					return scale.x * 80f;
				}
				return _length;
			}
			set {
				_length = value;
				var s = value / 80f;
				transform.localScale = new Vector3(s, s, s);
			}
		}

		private float _length = 80f;

		[Range(0, 1f)]
		[Tooltip("Damping on each turn while moving.")]
		public float Damping = 0.9879f;

		[Range(-180f, 180f)]
		[Tooltip("Maximal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMax;

		[Range(-180f, 180f)]
		[Tooltip("Minimal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMin;

		public bool ShowBracket {
			get {
				foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
					// todo use a component instead of relying on mesh names
					switch (mf.sharedMesh.name) {
						case BracketMeshName:
							return mf.gameObject.activeInHierarchy;
					}
				}
				return false;
			}
		}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Spinner;
		public override string ItemName => "Spinner";

		public override SpinnerData InstantiateData() => new();

		public override bool HasProceduralMesh => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<SpinnerData, SpinnerComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<SpinnerData, SpinnerComponent>);

		private const string BracketMeshName = "Spinner (Bracket)";
		public const string SwitchItem = "spinner_switch";

		#endregion

		#region Runtime

		[NonSerialized]
		private IRotatableAnimationComponent[] _animatedComponents;

		public SpinnerApi SpinnerApi { get; private set; }

		private void Awake()
		{
			Player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			SpinnerApi = new SpinnerApi(gameObject, Player, physicsEngine);

			Player.Register(SpinnerApi, this);
			RegisterPhysics(physicsEngine);

			_animatedComponents = GetComponentsInChildren<SpinnerPlateAnimationComponent>()
				.Select(gwa => gwa as IRotatableAnimationComponent)
				.Concat(GetComponentsInChildren<SpinnerLeverAnimationComponent>().Select(gwa => gwa as IRotatableAnimationComponent))
				.ToArray();
		}

		private void Start()
		{
			_playfieldToWorld = Player.PlayfieldToWorldMatrix;
		}

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { IsPulseSwitch = true }
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		public float4x4 TransformationWithinPlayfield => transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(SpinnerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = new Vector3(data.Center.X, data.Center.Y, data.Height);
			Length = data.Length;
			Rotation = data.Rotation;

			// spinner props
			Damping = data.Damping;
			AngleMax = data.AngleMax;
			AngleMin = data.AngleMin;

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.sharedMesh.name) {
					case BracketMeshName:
						mf.gameObject.SetActive(data.IsVisible && data.ShowBracket);
						break;
					default:
						mf.gameObject.SetActive(data.IsVisible);
						break;
				}
			}

			// collider data
			var collComponent = GetComponent<SpinnerColliderComponent>();
			if (collComponent) {
				collComponent.Elasticity = data.Elasticity;
				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(SpinnerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// surface
			ParentToSurface(data.Surface, data.Center, components);
			return Array.Empty<MonoBehaviour>();
		}

		public override SpinnerData CopyDataTo(SpinnerData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = new Vertex2D(Position.x, Position.y);
			data.Height = Position.z;
			data.Length = Length;
			data.Rotation = Rotation;

			// spinner props
			data.Damping = Damping;
			data.AngleMax = AngleMax;
			data.AngleMin = AngleMin;

			// visibility
			var isBracketActive = false;
			var isAnythingElseActive = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.sharedMesh.name) {
					case BracketMeshName:
						isBracketActive = mf.gameObject.activeInHierarchy;
						break;
					default:
						isAnythingElseActive = isAnythingElseActive || mf.gameObject.activeInHierarchy;
						break;
				}
			}
			data.IsVisible = isAnythingElseActive || isBracketActive;
			data.ShowBracket = isBracketActive;

			var collComponent = GetComponent<SpinnerColliderComponent>();
			if (collComponent) {
				data.Elasticity = collComponent.Elasticity;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var srcMainComp = go.GetComponent<SpinnerComponent>();
			if (srcMainComp) {
				Damping = srcMainComp.Damping;
				AngleMax = srcMainComp.AngleMax;
				AngleMin = srcMainComp.AngleMin;
			}
		}

		#endregion

		#region State

		internal SpinnerState CreateState()
		{
			// physics collision data
			var collComponent = GetComponent<SpinnerColliderComponent>();
			var staticData = collComponent
				? new SpinnerStaticState {
					AngleMax = math.radians(AngleMax),
					AngleMin = math.radians(AngleMin),
					Damping = math.pow(Damping, (float)PhysicsConstants.PhysFactor),
					Elasticity = collComponent.Elasticity,
					Height = Position.z
				} : default;

			// animation
			var animComponent = GetComponentInChildren<SpinnerPlateAnimationComponent>();
			var movementData = animComponent
				? new SpinnerMovementState {
					Angle = math.radians(math.clamp(0.0f, AngleMin, AngleMax)),
					AngleSpeed = 0f
				} : default;

			return new SpinnerState(
				animComponent ? animComponent.gameObject.GetInstanceID() : 0,
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
