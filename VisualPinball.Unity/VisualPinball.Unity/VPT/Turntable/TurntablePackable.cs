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
	public class TurntablePackable
	{
		public float Radius;
		public float MaxSpeed;
		public float SpinUp;
		public float SpinDown;
		public bool MotorOnStart;
		public bool SpinClockwise;

		public static byte[] Pack(TurntableComponent comp)
		{
			return PackageApi.Packer.Pack(new TurntablePackable {
				Radius = comp.Radius,
				MaxSpeed = comp.MaxSpeed,
				SpinUp = comp.SpinUp,
				SpinDown = comp.SpinDown,
				MotorOnStart = comp.MotorOnStart,
				SpinClockwise = comp.SpinClockwise
			});
		}

		public static void Unpack(byte[] bytes, TurntableComponent comp)
		{
			var data = PackageApi.Packer.Unpack<TurntablePackable>(bytes);
			comp.Radius = data.Radius;
			comp.MaxSpeed = data.MaxSpeed;
			comp.SpinUp = data.SpinUp;
			comp.SpinDown = data.SpinDown;
			comp.MotorOnStart = data.MotorOnStart;
			comp.SpinClockwise = data.SpinClockwise;
		}
	}
}
