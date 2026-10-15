// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// What a render pipeline needs to do so a camera can render through a cabinet projection
	/// (an off-centre frustum, see <see cref="CabinetProjectionSolver"/>). Implemented by the HDRP and URP
	/// integration packages, which register it before any scene loads.
	/// </summary>
	public interface ICabinetProjectionAdapter
	{
		/// <summary>The camera starts rendering through cabinet projections.</summary>
		void Attach(Camera camera);

		/// <summary>
		/// Sets the camera metadata the pipeline reads besides the projection matrix (field of view and
		/// aspect). Called after the pose and clip planes, before the projection matrix is assigned.
		/// </summary>
		void ApplyMetadata(Camera camera, in CabinetProjectionResult projection);

		/// <summary>The projection matrix was assigned. Reset temporal history when the view jumped.</summary>
		void ProjectionChanged(Camera camera, in CabinetProjectionResult projection, bool discontinuous);

		/// <summary>The camera is back to its own projection.</summary>
		void Detach(Camera camera);

		/// <summary>
		/// The anti-aliasing the pipeline can actually use for a camera with a cabinet projection, with the reason
		/// when it differs from the requested one.
		/// </summary>
		VpeAntiAliasing EffectiveAntiAliasing(VpeAntiAliasing requested, out string reason);
	}

	/// <summary>Connects the cabinet projection to the active render pipeline's adapter.</summary>
	public static class CabinetProjection
	{
		private static ICabinetProjectionAdapter _adapter;
		private static Camera _camera;

		public static void Register(ICabinetProjectionAdapter adapter) => _adapter = adapter;

		/// <summary>The active pipeline's adapter, or null when the pipeline doesn't support the mode.</summary>
		public static ICabinetProjectionAdapter Adapter => _adapter;

		/// <summary>The camera currently rendering through a cabinet projection, if any.</summary>
		public static Camera ActiveCamera => _camera != null ? _camera : null;

		public static bool IsActive(Camera camera) => camera != null && _camera == camera;

		/// <summary>The anti-aliasing a camera gets: the requested one, unless cabinet projection restricts it.</summary>
		public static VpeAntiAliasing EffectiveAntiAliasing(Camera camera, VpeAntiAliasing requested)
			=> IsActive(camera) && _adapter != null ? _adapter.EffectiveAntiAliasing(requested, out _) : requested;

		internal static void SetActive(Camera camera) => _camera = camera;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => _camera = null;
	}
}
