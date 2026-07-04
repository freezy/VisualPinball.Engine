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
	public struct PhysicsEnginePackable
	{
		public float GravityStrength;
		public bool HasKeyboardNudgeSettings;
		public KeyboardNudgeMode KeyboardNudgeMode;
		public float KeyboardNudgeStrength;

		public static byte[] Pack(PhysicsEngine comp)
		{
			return PackageApi.Packer.Pack(new PhysicsEnginePackable {
				GravityStrength = comp.GravityStrength,
				HasKeyboardNudgeSettings = true,
				KeyboardNudgeMode = comp.KeyboardNudgeMode,
				KeyboardNudgeStrength = comp.KeyboardNudgeStrength,
			});
		}

		public static void Unpack(byte[] bytes, PhysicsEngine comp)
		{
			var data = PackageApi.Packer.Unpack<PhysicsEnginePackable>(bytes);
			comp.GravityStrength = data.GravityStrength;
			comp.ConfigureKeyboardNudge(
				data.HasKeyboardNudgeSettings ? data.KeyboardNudgeMode : KeyboardNudgeMode.CabModel,
				data.HasKeyboardNudgeSettings ? data.KeyboardNudgeStrength : 1f
			);
		}
	}
}
