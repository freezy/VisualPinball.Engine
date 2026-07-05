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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Packaged representation of table-authored tilt-bob behavior.
	/// </summary>
	public struct TiltBobPackable
	{
		public float Damping;
		public float ThresholdAngle;

		public static byte[] Pack(TiltBobComponent comp)
		{
			comp.NormalizeSettings();
			return PackageApi.Packer.Pack(new TiltBobPackable {
				Damping = comp.PlumbDamping,
				ThresholdAngle = comp.PlumbThresholdAngle
			});
		}

		public static void Unpack(byte[] bytes, TiltBobComponent comp)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}

			var data = PackageApi.Packer.Unpack<TiltBobPackable>(bytes);
			comp.PlumbDamping = data.Damping;
			comp.PlumbThresholdAngle = data.ThresholdAngle;
			comp.NormalizeSettings();
		}
	}
}
