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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Packaged representation of <see cref="PhysicsEngine"/> settings.
	/// </summary>
	/// <remarks>
	/// The boolean "Has..." flags preserve backward compatibility with packages
	/// created before keyboard nudge, plumb, damping, or visual nudge settings were
	/// serialized. Plumb fields are retained only so older packages can be read;
	/// new packages route tilt through <see cref="TiltBobComponent"/> plus player
	/// cabinet settings instead of table-owned physics settings.
	/// </remarks>
	public struct PhysicsEnginePackable
	{
		public float GravityStrength;
		public bool HasKeyboardNudgeSettings;
		public KeyboardNudgeMode KeyboardNudgeMode;
		public float KeyboardNudgeStrength;
		public bool HasKeyboardCabinetDamping;
		public float KeyboardCabinetDamping;
		public bool HasPlumbSettings;
		public bool SimulatedPlumb;
		public float PlumbDamping;
		public float PlumbThresholdAngle;
		public bool HasVisualNudgeSettings;
		public float VisualNudgeStrength;

		/// <summary>
		/// Serializes physics engine configuration for a table package.
		/// </summary>
		public static byte[] Pack(PhysicsEngine comp)
		{
			return PackageApi.Packer.Pack(new PhysicsEnginePackable {
				GravityStrength = comp.GravityStrength,
				HasKeyboardNudgeSettings = true,
				KeyboardNudgeMode = comp.KeyboardNudgeMode,
				KeyboardNudgeStrength = comp.KeyboardNudgeStrength,
				HasKeyboardCabinetDamping = true,
				KeyboardCabinetDamping = comp.KeyboardCabinetDamping,
				HasPlumbSettings = false,
				SimulatedPlumb = false,
				PlumbDamping = 1f,
				PlumbThresholdAngle = 2f,
				HasVisualNudgeSettings = true,
				VisualNudgeStrength = comp.VisualNudgeStrength,
			});
		}

		/// <summary>
		/// Restores physics engine configuration from package data.
		/// </summary>
		public static void Unpack(byte[] bytes, PhysicsEngine comp)
		{
			var data = PackageApi.Packer.Unpack<PhysicsEnginePackable>(bytes);
			comp.GravityStrength = data.GravityStrength;
			comp.ConfigureNudge(new CabinetNudgeSettings {
				keyboardMode = (int)(data.HasKeyboardNudgeSettings ? data.KeyboardNudgeMode : KeyboardNudgeMode.CabModel),
				keyboardStrength = data.HasKeyboardNudgeSettings ? data.KeyboardNudgeStrength : 1f,
				keyboardCabinetDamping = data.HasKeyboardCabinetDamping
					? data.KeyboardCabinetDamping
					: CabinetPhysicsState.DefaultKeyboardDampingRatio,
				visualStrength = data.HasVisualNudgeSettings ? data.VisualNudgeStrength : 1f
			});
		}
	}
}
