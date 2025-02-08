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

// ReSharper disable MemberCanBePrivate.Global

namespace VisualPinball.Unity
{
	public struct SpinnerPackable
	{
		public float Damping;
		public float AngleMax;
		public float AngleMin;

		public static byte[] Pack(SpinnerComponent comp)
		{
			return PackageApi.Packer.Pack(new SpinnerPackable {
				Damping = comp.Damping,
				AngleMax = comp.AngleMax,
				AngleMin = comp.AngleMin,
			});
		}

		public static void Unpack(byte[] bytes, SpinnerComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SpinnerPackable>(bytes);
			comp.Damping = data.Damping;
			comp.AngleMax = data.AngleMax;
			comp.AngleMin = data.AngleMin;
		}
	}
}
