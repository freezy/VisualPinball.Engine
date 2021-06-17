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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// An asset containing the flipper correction parameters (aka nFozzy).
	/// </summary>
	[CreateAssetMenu(fileName = "Flipper Correction", menuName = "Visual Pinball/Flipper Correction", order = 101)]
	public class FlipperCorrectionAsset : ScriptableObject
	{
		public AnimationCurve Polarities = AnimationCurve.Linear(0, 0, 1, 1);
		[HideInInspector]
		[Tooltip("The curve will be sliced in smaller straight lines. The bigger, the more precise, but at memory cost.")]
		[Min(1)]
		public int PolaritiesCurveSlicingCount = 256;

		public AnimationCurve Velocities = AnimationCurve.Linear(0, 0, 1, 1);
		[HideInInspector]
		[Tooltip("The curve will be sliced in smaller straight lines. The bigger, the more precise, but at memory cost.")]
		[Min(1)]
		public int VelocitiesCurveSlicingCount = 256;

		[Tooltip("Time since flipper fire, in ms, after which the corrections are not applied anymore.")]
		public uint TimeThresholdMs = 60;

	}
}
