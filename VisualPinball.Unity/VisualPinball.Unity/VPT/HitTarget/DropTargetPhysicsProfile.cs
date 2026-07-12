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

using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("DropTargetPhysicsProfile")]
	[CreateAssetMenu(fileName = "DropTargetPhysicsProfile", menuName = "Pinball/Drop Target Physics Profile", order = 101)]
	public class DropTargetPhysicsProfile : ScriptableObject
	{
		[Tooltip("Human-readable mechanism or parts family represented by this profile.")]
		public string MechanismName = "Provisional generic drop target";

		[Tooltip("Measured means that the profile was fitted to recorded real-machine motion and held-out shots.")]
		public DropTargetProfileCalibration Calibration = DropTargetProfileCalibration.Provisional;

		[TextArea]
		[Tooltip("Measurement rig, source data, fit version, and validation notes. Required for measured profiles.")]
		public string CalibrationSource;

		public DropTargetMechanicalConfig Config = DropTargetMechanicalConfig.Default;
	}
}
