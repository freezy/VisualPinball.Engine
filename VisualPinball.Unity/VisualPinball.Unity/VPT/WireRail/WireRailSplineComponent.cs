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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using UnityEngine;
using UnityEngine.Splines;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Marks the generated spline child of a <see cref="WireRailComponent"/>. The Wire Rail
	/// resolves its spline through this marker rather than by name, and locks the child's
	/// transform because it carries the VPX-to-world conversion the generated geometry relies on.
	///
	/// Unlike drag-point splines, this child holds the authored route itself, so it is also the
	/// packable that carries the route through a .vpe package: the child stays in the package's
	/// scene (its transform, mesh and material travel with the glTF), and this component
	/// restores the <see cref="SplineContainer"/> on load.
	/// </summary>
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	[PackAs("WireRailSpline")]
	public sealed class WireRailSplineComponent : MonoBehaviour, IPackable
	{
		/// <summary>
		/// True once a package restored this child's route. The owning Wire Rail waits for
		/// it before synchronizing layouts and fixtures against the route length.
		/// </summary>
		public bool SplineRestored { get; private set; }

		public byte[] Pack() => WireRailSplinePackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> Array.Empty<byte>();

		public void Unpack(byte[] bytes) => WireRailSplinePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs,
			PackagedFiles files)
		{
		}

		internal void RestoreSpline(Spline spline)
		{
			if (!TryGetComponent<SplineContainer>(out var container)) {
				container = gameObject.AddComponent<SplineContainer>();
			}
			container.Spline = spline;
			transform.hideFlags |= HideFlags.NotEditable;
			SplineRestored = true;
			// The hierarchy is inactive while a runtime package is being restored, so look
			// through inactive parents as well.
			GetComponentInParent<WireRailComponent>(true)?.OnSplineRestored();
		}
	}
}
