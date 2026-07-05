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
	public struct MagnetPackable
	{
		public float Radius;
		public float Strength;
		public MagnetForceProfile ForceProfile;
		public bool GrabBall;
		public float GrabRadius;
		public float HeightRange;
		public bool IsEnabledOnStart;
		public bool DrawDebugForces;

		public static byte[] Pack(MagnetComponent comp)
		{
			return PackageApi.Packer.Pack(new MagnetPackable {
				Radius = comp.Radius,
				Strength = comp.Strength,
				ForceProfile = comp.ForceProfile,
				GrabBall = comp.GrabBall,
				GrabRadius = comp.GrabRadius,
				HeightRange = comp.HeightRange,
				IsEnabledOnStart = comp.IsEnabledOnStart,
				DrawDebugForces = comp.DrawDebugForces,
			});
		}

		public static void Unpack(byte[] bytes, MagnetComponent comp)
		{
			var data = PackageApi.Packer.Unpack<MagnetPackable>(bytes);
			comp.Radius = data.Radius;
			comp.Strength = data.Strength;
			comp.ForceProfile = data.ForceProfile;
			comp.GrabBall = data.GrabBall;
			comp.GrabRadius = data.GrabRadius;
			comp.HeightRange = data.HeightRange;
			comp.IsEnabledOnStart = data.IsEnabledOnStart;
			comp.DrawDebugForces = data.DrawDebugForces;
		}
	}
}
