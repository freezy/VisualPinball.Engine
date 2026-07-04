// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Applies a camera-space visual response to simulated cabinet motion.
	/// </summary>
	/// <remarks>
	/// This mirrors VP's visual nudge behavior in
	/// <c>vpinball/src/renderer/Renderer.cpp</c>, where cabinet displacement is
	/// represented by shifting the rendered view rather than moving every table
	/// object. VPE applies the offset to game cameras so authoring/editor cameras
	/// are not disturbed.
	/// </remarks>
	[DefaultExecutionOrder(10000)]
	public sealed class VisualNudgeComponent : MonoBehaviour
	{
		[NonSerialized] private readonly Dictionary<Camera, Vector3> _appliedOffsets = new();
		[NonSerialized] private readonly List<Camera> _staleCameras = new();
		[NonSerialized] private PhysicsEngine _physicsEngine;
		[NonSerialized] private PlayfieldComponent _playfield;
		[NonSerialized] private float2 _cabinetOffset;
		[NonSerialized] private float _strength = 1f;

		/// <summary>
		/// Connects this component to physics telemetry and sets the visual strength.
		/// </summary>
		internal void Configure(PhysicsEngine physicsEngine, float strength)
		{
			_physicsEngine = physicsEngine;
			_strength = math.clamp(strength, 0f, 2f);
			if (_playfield == null && _physicsEngine != null) {
				_playfield = _physicsEngine.GetComponentInParent<TableComponent>()?.GetComponentInChildren<PlayfieldComponent>();
			}
		}

		/// <summary>
		/// Updates the latest cabinet-space offset produced by the physics thread.
		/// </summary>
		internal void SetCabinetOffset(float2 cabinetOffset)
		{
			_cabinetOffset = cabinetOffset;
		}

		private void LateUpdate()
		{
			var desiredOffset = -WorldCabinetOffset() * _strength;
			ApplyToGameCameras(desiredOffset);
		}

		private void OnDisable() => RestoreAll();

		private void OnDestroy() => RestoreAll();

		private Vector3 WorldCabinetOffset()
		{
			var localOffset = new Vector3(_cabinetOffset.x, 0f, -_cabinetOffset.y);
			return _playfield != null ? _playfield.transform.TransformVector(localOffset) : localOffset;
		}

		/// <summary>
		/// Applies the desired offset to all game cameras and restores cameras that
		/// disappeared from the active list.
		/// </summary>
		private void ApplyToGameCameras(Vector3 desiredOffset)
		{
			_staleCameras.Clear();
			foreach (var camera in _appliedOffsets.Keys) {
				_staleCameras.Add(camera);
			}

			foreach (var camera in Camera.allCameras) {
				if (!ShouldOffset(camera)) {
					continue;
				}
				_staleCameras.Remove(camera);
				ApplyToCamera(camera, desiredOffset);
			}

			foreach (var camera in _staleCameras) {
				RestoreCamera(camera);
			}
			_staleCameras.Clear();
		}

		private static bool ShouldOffset(Camera camera)
		{
			return camera != null
			       && camera.enabled
			       && camera.cameraType == CameraType.Game;
		}

		/// <summary>
		/// Replaces this frame's previous offset with the new one.
		/// </summary>
		/// <remarks>
		/// The previous offset is subtracted first so animated camera rigs, head
		/// tracking, or other camera scripts can still move the camera normally
		/// between VPE visual nudge updates.
		/// </remarks>
		private void ApplyToCamera(Camera camera, Vector3 desiredOffset)
		{
			if (_appliedOffsets.TryGetValue(camera, out var previousOffset)) {
				camera.transform.position -= previousOffset;
			}

			if (desiredOffset.sqrMagnitude < 1e-12f) {
				_appliedOffsets.Remove(camera);
				return;
			}

			camera.transform.position += desiredOffset;
			_appliedOffsets[camera] = desiredOffset;
		}

		private void RestoreCamera(Camera camera)
		{
			if (camera != null && _appliedOffsets.TryGetValue(camera, out var offset)) {
				camera.transform.position -= offset;
			}
			_appliedOffsets.Remove(camera);
		}

		private void RestoreAll()
		{
			_staleCameras.Clear();
			foreach (var camera in _appliedOffsets.Keys) {
				_staleCameras.Add(camera);
			}
			foreach (var camera in _staleCameras) {
				RestoreCamera(camera);
			}
			_staleCameras.Clear();
		}
	}
}
