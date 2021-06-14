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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Makes flippers flip better, A.K.A. nFozzy physics.
	/// </summary>
	public class FlipperCorrectionAuthoring : MonoBehaviour
	{
		public AnimationCurveAsset Polarities;

		[Tooltip("The curve will be sliced in smaller straight lines. The bigger, the more precise, but at memory cost.")]
		[Min(1)]
		public int PolaritiesCurveSlicingCount = 256;

		public AnimationCurveAsset Velocities;

		[Tooltip("The curve will be sliced in smaller straight lines. The bigger, the more precise, but at memory cost.")]
		[Min(1)]
		public int VelocitiesCurveSlicingCount = 256;

		[Tooltip("Time since flipper fire, in ms, after which the corrections are not applied anymore.")]
		public uint TimeThresholdMs = 60;
	}
}
