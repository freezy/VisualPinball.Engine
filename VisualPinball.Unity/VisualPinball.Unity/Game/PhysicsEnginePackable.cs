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
	/// serialized. Missing fields are restored with the same defaults used by new
	/// components.
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
				HasPlumbSettings = true,
				SimulatedPlumb = comp.SimulatedPlumb,
				PlumbDamping = comp.PlumbDamping,
				PlumbThresholdAngle = comp.PlumbThresholdAngle,
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
			comp.ConfigureKeyboardNudge(
				data.HasKeyboardNudgeSettings ? data.KeyboardNudgeMode : KeyboardNudgeMode.CabModel,
				data.HasKeyboardNudgeSettings ? data.KeyboardNudgeStrength : 1f,
				data.HasKeyboardCabinetDamping ? data.KeyboardCabinetDamping : CabinetPhysicsState.DefaultKeyboardDampingRatio
			);
			comp.ConfigurePlumb(
				data.HasPlumbSettings ? data.SimulatedPlumb : true,
				data.HasPlumbSettings ? data.PlumbDamping : 1f,
				data.HasPlumbSettings ? data.PlumbThresholdAngle : 2f
			);
			comp.ConfigureVisualNudge(data.HasVisualNudgeSettings ? data.VisualNudgeStrength : 1f);
		}
	}
}
