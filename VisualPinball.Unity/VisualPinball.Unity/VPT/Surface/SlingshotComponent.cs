// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Slingshot")]
	public class SlingshotComponent : MonoBehaviour, IMeshComponent, IMainRenderableComponent, IRubberData
	{
		[Tooltip("Reference to the wall that acts as slingshot.")]
		public SurfaceColliderComponent SlingshotSurface;

		[Tooltip("Reference to the rubber at \"enabled\" position (coil on).")]
		public RubberComponent RubberOn;

		[Tooltip("Reference to the rubber at \"disabled\" position (coil off).")]
		public RubberComponent RubberOff;

		[Tooltip("Total duration of the animation in milliseconds.")]
		public float AnimationDuration = 200f;

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

		private const int MaxNumMeshCaches = 15;

		#region Runtime

		private void Awake()
		{
			_meshGenerator = new RubberMeshGenerator(this);
		}

		private void Start()
		{
			var player = GetComponentInParent<Player>();
			if (!player || player.TableApi == null || !SlingshotSurface) {
				return;
			}
			var slingshotSurfaceApi = player.TableApi.Surface(SlingshotSurface.MainComponent);
			slingshotSurfaceApi.Slingshot += OnSlingshot;
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

		#region IMeshComponent

		public IMainRenderableComponent MainRenderableComponent => this;

		public void RebuildMeshes()
		{
			var mf = GetComponent<MeshFilter>();
			if (!mf) {
				Debug.LogWarning("Mesh filter or renderer not found.");
				return;
			}

			var mesh = GetMesh();

			if (mesh != null) {
				mf.sharedMesh = mesh;
			}
		}

		private Mesh GetMesh()
		{
			var pos = (int)(Position * MaxNumMeshCaches);
			if (_meshes.ContainsKey(pos)) {
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

			var mesh = _meshGenerator
				.GetTransformedMesh(pf.PlayfieldHeight, r0.Height, pf.PlayfieldDetailLevel)
				.ToUnityMesh();

			_meshes[pos] = mesh;

			return mesh;
		}

		#endregion

		#region IMainRenderableComponent

		public bool IsLocked { get => _isLocked; set => _isLocked = value; }
		public bool CanBeTransformed => false;
		public string ItemName => "Slingshot";
		public Entity Entity { get; set; }

		public void UpdateTransforms() { }
		public void UpdateVisibility() { }

		public ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public Vector3 GetEditorPosition() => Vector3.zero;
		public void SetEditorPosition(Vector3 pos) { }

		public ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public Vector3 GetEditorRotation() => Vector3.zero;
		public void SetEditorRotation(Vector3 pos) { }

		public ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public Vector3 GetEditorScale() => Vector3.one;
		public void SetEditorScale(Vector3 pos) { }

		#endregion

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
	}
}
