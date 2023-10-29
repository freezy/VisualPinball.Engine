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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Spinner")]
	public class SpinnerComponent : MainRenderableComponent<SpinnerData>,
		ISwitchDeviceComponent, IOnSurfaceComponent
	{
		#region Data

		[Tooltip("Position of the spinner on the playfield.")]
		public Vector2 Position;

		[Tooltip("Z-Position on the playfield.")]
		public float Height = 60f;

		[Range(-180f, 180f)]
		[Tooltip("Z-Axis rotation of the spinner on the playfield.")]
		public float Rotation;

		[Min(0)]
		[Tooltip("Overall scaling of the spinner")]
		public float Length = 80f;

		[Range(0, 1f)]
		[Tooltip("Damping on each turn while moving.")]
		public float Damping = 0.9879f;

		[Range(-180f, 180f)]
		[Tooltip("Maximal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMax;

		[Range(-180f, 180f)]
		[Tooltip("Minimal angle. This allows the spinner to bounce back instead of executing a 360° rotation.")]
		public float AngleMin;

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this spinner is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Spinner;
		public override string ItemName => "Spinner";

		public override SpinnerData InstantiateData() => new SpinnerData();

		public override bool HasProceduralMesh => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<SpinnerData, SpinnerComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<SpinnerData, SpinnerComponent>);

		private const string BracketMeshName = "Spinner (Bracket)";
		public const string SwitchItem = "spinner_switch";

		#endregion

		#region Runtime

		public SpinnerApi SpinnerApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			SpinnerApi = new SpinnerApi(gameObject, player, physicsEngine);

			player.Register(SpinnerApi, this);
			RegisterPhysics(physicsEngine);
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

		public void OnSurfaceUpdated() => UpdateTransforms();
		public float PositionZ => SurfaceHeight(Surface, Position);

		public float HeightOnPlayfield => Height + PositionZ;

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// position
			t.localPosition = Physics.TranslateToWorld(Position.x, Position.y, HeightOnPlayfield);

			// scale
			t.localScale = Physics.ScaleToWorld(Length, Length, Length);

			// rotation
			t.localEulerAngles = Physics.RotateToWorld(0, 0, Rotation);
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(SpinnerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityFloat2();
			Height = data.Height;
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
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);
			return Array.Empty<MonoBehaviour>();
		}

		public override SpinnerData CopyDataTo(SpinnerData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Height = Height;
			data.Length = Length;
			data.Rotation = Rotation;
			data.Surface = Surface != null ? Surface.name : string.Empty;

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
			var spinnerComponent = go.GetComponent<SpinnerComponent>();
			if (spinnerComponent != null) {
				Position = spinnerComponent.Position;
				Height = spinnerComponent.Height;
				Rotation = spinnerComponent.Rotation;
				Length = spinnerComponent.Length;
				Damping = spinnerComponent.Damping;
				AngleMax = spinnerComponent.AngleMax;
				AngleMin = spinnerComponent.AngleMin;
				Surface = spinnerComponent.Surface;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
				Rotation = go.transform.localEulerAngles.z;
			}

			UpdateTransforms();
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
					Height = Height
				} : default;

			// animation
			var animComponent = GetComponentInChildren<SpinnerPlateAnimationComponent>();
			var movementData = animComponent
				? new SpinnerMovementState {
					Angle = math.radians(math.clamp(0.0f, AngleMin, AngleMax)),
					AngleSpeed = 0f
				} : default;

			return new SpinnerState(
				collComponent ? gameObject.GetInstanceID() : 0,
				animComponent ? animComponent.gameObject.GetInstanceID() : 0,
				staticData,
				movementData
			);
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition()
		{
			return new Vector3(Position.x, Position.y, Height);
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			Position = pos;
			Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = ClampDegrees(rot.x);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;

		public bool ShowBracket {
			get {
				foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
					switch (mf.sharedMesh.name) {
						case BracketMeshName:
							return mf.gameObject.activeInHierarchy;
					}
				}
				return false;
			}
		}

		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Length = scale.x;

		#endregion
	}
}
