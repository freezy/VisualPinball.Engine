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
using System.Collections;
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

	[AddComponentMenu("Visual Pinball/Game Item/Slingshot")]
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
			StopAllCoroutines();
			StartCoroutine(nameof(Animate));
		}

		private IEnumerator Animate()
		{
			var duration = AnimationDuration / 1000;
			var journey = 0f;
			while (journey <= duration) {

				journey += Time.deltaTime;
				var curvePercent = AnimationCurve.Evaluate(journey / duration);
				Position = math.clamp(curvePercent, 0f, 1f);

				RebuildMeshes();

				yield return null;
			}
		}

		#endregion

		#region IRubberData

		public DragPointData[] DragPoints => DragPointsAt(Position);
		public int Thickness => RubberOff.GetComponent<RubberComponent>()?.Thickness ?? 8;
		public float Height => RubberOff.GetComponent<RubberComponent>()?.Height ?? 25f;
		public float RotX => RubberOff.GetComponent<RubberComponent>()?.RotX ?? 0;
		public float RotY => RubberOff.GetComponent<RubberComponent>()?.RotY ?? 0;
		public float RotZ => RubberOff.GetComponent<RubberComponent>()?.RotZ ?? 0;

		#endregion

		public IMainRenderableComponent MainRenderableComponent => this;
		public void UpdateTransforms() { }

		public void RebuildMeshes()
		{
			var mf = GetComponent<MeshFilter>();
			var mr = GetComponent<MeshRenderer>();
			if (!mf || !mr) {
				Debug.LogWarning("Mesh filter or renderer not found.");
				return;
			}

			// mesh
			var mesh = GetMesh();
			if (mesh != null) {
				mf.sharedMesh = mesh;
			}

			// material
			if (RubberOff && !mr.sharedMaterial) {
				var rubberMr = RubberOff.GetComponent<MeshRenderer>();
				if (rubberMr) {
					mr.sharedMaterial = rubberMr.sharedMaterial;
				}
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
			if (Application.isPlaying && _meshes.ContainsKey(pos)) {
				return _meshes[pos];
			}

			if (!RubberOff || DragPoints.Length < 3) {
				return null;
			}

			var pf = GetComponentInParent<PlayfieldComponent>();
			var r0 = RubberOff.GetComponent<RubberComponent>();
			if (!r0 || !pf) {
				return null;
			}

			Debug.Log($"Generating new mesh at {pos}");

			var mesh = MeshGenerator
				.GetTransformedMesh(0, r0.Height, pf.PlayfieldDetailLevel)
				.TransformToWorld()
				.ToUnityMesh();

			mesh.name = $"{name} (Mesh)";
			_meshes[pos] = mesh;

			return mesh;
		}

		public static GameObject LoadPrefab() => Resources.Load<GameObject>("Prefabs/Slingshot");

		private DragPointData[] DragPointsAt(float pos)
		{
			if (RubberOn == null || RubberOff == null) {
				Debug.LogWarning("Rubber references not set.");
				return Array.Empty<DragPointData>();
			}
			var r0 = RubberOff.GetComponent<RubberComponent>();
			var r1 = RubberOn.GetComponent<RubberComponent>();
			if (r0 == null || r1 == null || r0.DragPoints == null || r1.DragPoints == null) {
				Debug.LogWarning("Rubber references not found or drag points not set.");
				return Array.Empty<DragPointData>();
			}

			var dp0 = r0.DragPoints;
			var dp1 = r1.DragPoints;

			if (dp0.Length != dp1.Length) {
				Debug.LogWarning($"Drag point number varies ({dp0.Length} vs {dp1.Length}.).");
				return Array.Empty<DragPointData>();
			}

			var dp = new DragPointData[dp0.Length];
			for (var i = 0; i < dp.Length; i++) {
				dp[i] = dp0[i].Lerp(dp1[i], pos);
			}

			return dp;
		}

		public void CopyFromObject(GameObject go)
		{
			// don't think we can do much here, since it's not a logical, not visible component, and the
			// actual data lies in the children.
		}
	}
}
