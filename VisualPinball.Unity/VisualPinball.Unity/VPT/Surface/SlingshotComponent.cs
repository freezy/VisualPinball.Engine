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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	public enum Axis
	{
		X, Y, Z
	}

	[AddComponentMenu("Pinball/Game Item/Slingshot")]
	public class SlingshotComponent : MonoBehaviour, IMeshComponent, IMainRenderableComponent, IRubberData, ISwitchDeviceComponent
	{
		[Tooltip("Reference to the wall that acts as slingshot.")]
		public SurfaceColliderComponent SlingshotSurface;

		[Tooltip("Reference to the rubber at \"enabled\" position (coil on).")]
		public RubberComponent RubberOn;

		[Tooltip("Reference to the rubber at \"disabled\" position (coil off).")]
		public RubberComponent RubberOff;

		[Tooltip("Reference to the arm attached to the coil. Rotates around X.")]
		public GameObject CoilArm;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the coil arm when off.")]
		public float CoilArmStartAngle;

		[FormerlySerializedAs("CoilArmAngle")]
		[Range(-180f, 180f)]
		[Tooltip("Angle of the coil arm when on.")]
		public float CoilArmEndAngle;

		[Tooltip("Which axis to rotate.")]
		public Axis CoilArmRotationAxis;

		[Min(0f)]
		[Tooltip("Total duration of the animation in milliseconds.")]
		public float AnimationDuration = 70f;

		[Tooltip("Animation curve. Starts at 0 and ends at 0.")]
		public AnimationCurve AnimationCurve = new AnimationCurve(
			new Keyframe(0, 0),
			new Keyframe(0.5f, 1, 3.535f, 0f, 0.03333336f, 0.5416666f),
			new Keyframe(1, 0)
		);

		[NonSerialized] public float Position;
		[SerializeField] private bool _isLocked;
		[NonSerialized] private readonly Dictionary<int, Mesh> _meshes = new Dictionary<int, Mesh>();
		[NonSerialized] private RubberMeshGenerator _meshGenerator;
		[NonSerialized] private bool _isAnimating;
		[NonSerialized] private float _animationJourney;
		[NonSerialized] private DragPointData[] _dragPointsBuffer = Array.Empty<DragPointData>();
		[NonSerialized] private MeshFilter _meshFilter;
		[NonSerialized] private MeshRenderer _meshRenderer;
		[NonSerialized] private MeshRenderer _rubberOffMeshRenderer;
		[NonSerialized] private PlayfieldComponent _playfield;
		[NonSerialized] private bool _loggedMissingMeshComponents;
		[NonSerialized] private bool _loggedMissingRubberReferences;
		[NonSerialized] private bool _loggedMismatchedDragPoints;
		private RubberMeshGenerator MeshGenerator => _meshGenerator ??= new RubberMeshGenerator(this);

		private const int MaxNumMeshCaches = 15;

		private const string SlingshotSwitchItem = "slingshot_switch";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SlingshotSwitchItem)  {
				IsPulseSwitch = true
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#region Runtime

		public SlingshotApi SlingshotApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			_meshFilter = GetComponent<MeshFilter>();
			_meshRenderer = GetComponent<MeshRenderer>();
			_playfield = GetComponentInParent<PlayfieldComponent>();
			_rubberOffMeshRenderer = RubberOff ? RubberOff.GetComponent<MeshRenderer>() : null;
			SlingshotApi = new SlingshotApi(gameObject, player, physicsEngine);

			player.Register(SlingshotApi, this);
		}

		private void Start()
		{
			var player = GetComponentInParent<Player>();
			if (!player || player.TableApi == null || !SlingshotSurface) {
				return;
			}
			var slingshotSurfaceApi = player.TableApi.Surface(SlingshotSurface.MainComponent);
			if (slingshotSurfaceApi != null) {
				slingshotSurfaceApi.Slingshot += OnSlingshot;
			}

			PrewarmMeshes();
		}

		private void OnDestroy()
		{
			var player = GetComponentInParent<Player>();
			if (player && player.TableApi != null && SlingshotSurface) {
				var slingshotSurfaceApi = player.TableApi.Surface(SlingshotSurface.MainComponent);
				slingshotSurfaceApi.Slingshot -= OnSlingshot;
			}

			_meshes.Clear();
		}

		private void OnSlingshot(object sender, EventArgs e) => TriggerAnimation();

		private void TriggerAnimation()
		{
			_animationJourney = 0f;
			_isAnimating = true;
		}

		private void Update()
		{
			if (!_isAnimating) {
				return;
			}

			var duration = AnimationDuration / 1000;
			if (duration <= 0f) {
				Position = 0f;
				RebuildMeshes();
				_isAnimating = false;
				return;
			}

			_animationJourney += Time.deltaTime;
			var curvePercent = AnimationCurve.Evaluate(_animationJourney / duration);
			Position = math.clamp(curvePercent, 0f, 1f);
			RebuildMeshes();

			if (_animationJourney > duration) {
				Position = 0f;
				RebuildMeshes();
				_isAnimating = false;
			}
		}

		#endregion

		#region IRubberData

		public DragPointData[] DragPoints => DragPointsAt(Position);
		public int Thickness => RubberOff ? RubberOff.Thickness : 8;
		public float Height => RubberOff ? RubberOff.Height : 25f;

		#endregion

		public IMainRenderableComponent MainRenderableComponent => this;
		public void UpdateTransforms() { }
		public void UpdateVisibility() { }

		public void RebuildMeshes()
		{
			if (!_meshFilter || !_meshRenderer) {
				LogConfigurationWarningOnce(ref _loggedMissingMeshComponents, "Mesh filter or renderer not found.");
				return;
			}

			// mesh
			var mesh = GetMesh();
			if (mesh != null) {
				_meshFilter.sharedMesh = mesh;
			}

			// material
			if (_rubberOffMeshRenderer && !_meshRenderer.sharedMaterial) {
				_meshRenderer.sharedMaterial = _rubberOffMeshRenderer.sharedMaterial;
			}

			if (CoilArm) {
				var currentRot = CoilArm.transform.rotation.eulerAngles;
				switch (CoilArmRotationAxis) {
					case Axis.X:
						CoilArm.transform.rotation = Quaternion.Euler(CoilArmStartAngle + CoilArmEndAngle * Position, currentRot.y, currentRot.z);
						break;
					case Axis.Y:
						CoilArm.transform.rotation = Quaternion.Euler(currentRot.x, CoilArmStartAngle + CoilArmEndAngle * Position, currentRot.z);
						break;
					case Axis.Z:
						CoilArm.transform.rotation = Quaternion.Euler(currentRot.x, currentRot.y, CoilArmStartAngle + CoilArmEndAngle * Position);
						break;
				}
			}
		}

		private Mesh GetMesh()
		{
			var pos = (int)(Position * MaxNumMeshCaches);
			if (Application.isPlaying && _meshes.TryGetValue(pos, out var cachedMesh)) {
				return cachedMesh;
			}

			if (!TryGetSourceDragPoints(out var dp0, out var dp1) || dp0.Length < 3) {
				return null;
			}

			if (!_playfield) {
				return null;
			}

			var mesh = MeshGenerator
				.GetTransformedMesh(0, _playfield.PlayfieldDetailLevel)
				.TransformToWorld()
				.ToUnityMesh();

			mesh.name = $"{name} (Mesh)";
			_meshes[pos] = mesh;

			return mesh;
		}

		public static GameObject LoadPrefab() => Resources.Load<GameObject>("Prefabs/Slingshot");

		private DragPointData[] DragPointsAt(float pos)
		{
			if (!TryGetSourceDragPoints(out var dp0, out var dp1)) {
				return Array.Empty<DragPointData>();
			}

			if (dp0.Length != dp1.Length) {
				LogConfigurationWarningOnce(ref _loggedMismatchedDragPoints, $"Drag point number varies ({dp0.Length} vs {dp1.Length}.).");
				return Array.Empty<DragPointData>();
			}

			if (_dragPointsBuffer.Length != dp0.Length) {
				_dragPointsBuffer = new DragPointData[dp0.Length];
			}

			for (var i = 0; i < _dragPointsBuffer.Length; i++) {
				_dragPointsBuffer[i] = dp0[i].Lerp(dp1[i], pos);
			}

			return _dragPointsBuffer;
		}

		private bool TryGetSourceDragPoints(out DragPointData[] dp0, out DragPointData[] dp1)
		{
			dp0 = null;
			dp1 = null;

			if (RubberOn == null || RubberOff == null || RubberOn.DragPoints == null || RubberOff.DragPoints == null) {
				LogConfigurationWarningOnce(ref _loggedMissingRubberReferences, "Rubber references not found or drag points not set.");
				return false;
			}

			dp0 = RubberOff.DragPoints;
			dp1 = RubberOn.DragPoints;
			return true;
		}

		private void PrewarmMeshes()
		{
			if (!Application.isPlaying || !TryGetSourceDragPoints(out var dp0, out _) || dp0.Length < 3 || !_playfield) {
				return;
			}

			var previousPosition = Position;
			for (var pos = 0; pos <= MaxNumMeshCaches; pos++) {
				Position = (float)pos / MaxNumMeshCaches;
				GetMesh();
			}
			Position = previousPosition;
		}

		private void LogConfigurationWarningOnce(ref bool flag, string message)
		{
			if (flag) {
				return;
			}
			flag = true;
#if UNITY_EDITOR
			Debug.LogWarning(message);
#endif
		}

		public void CopyFromObject(GameObject go)
		{
			// don't think we can do much here, since it's not a logical, not visible component, and the
			// actual data lies in the children.
		}
	}
}
