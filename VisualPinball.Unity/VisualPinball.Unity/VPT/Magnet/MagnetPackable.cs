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
		private const int CurrentVersion = 2;

		public int Version;
		public float Radius;
		public float Strength;
		public MagnetType MagnetType;
		public MagnetForceProfile ForceProfile;
		public float CoilRiseTime;
		public float CoilFallTime;
		public float PoleRadius;
		public bool GrabBall;
		public float GrabRadius;
		public float CylinderRadius;
		public float CylinderHeight;
		public float CylindricalDamping;
		public bool GenerateCylinderCollider;
		public float HeightRange;
		public bool IsEnabledOnStart;
		public bool IsKinematic;
		public bool DrawDebugForces;

		public static byte[] Pack(MagnetComponent comp)
		{
			return PackageApi.Packer.Pack(new MagnetPackable {
				Version = CurrentVersion,
				Radius = comp.Radius,
				Strength = comp.Strength,
				MagnetType = comp.MagnetType,
				ForceProfile = comp.ForceProfile,
				CoilRiseTime = comp.CoilRiseTime,
				CoilFallTime = comp.CoilFallTime,
				PoleRadius = comp.PoleRadius,
				GrabBall = comp.GrabBall,
				GrabRadius = comp.GrabRadius,
				CylinderRadius = comp.CylinderRadius,
				CylinderHeight = comp.CylinderHeight,
				CylindricalDamping = comp.CylindricalDamping,
				GenerateCylinderCollider = comp.GenerateCylinderCollider,
				HeightRange = comp.HeightRange,
				IsEnabledOnStart = comp.IsEnabledOnStart,
				IsKinematic = comp.IsKinematic,
				DrawDebugForces = comp.DrawDebugForces,
			});
		}

		public static void Unpack(byte[] bytes, MagnetComponent comp)
		{
			var data = PackageApi.Packer.Unpack<MagnetPackable>(bytes);
			comp.Radius = data.Radius;
			comp.Strength = data.Strength;
			comp.MagnetType = data.MagnetType;
			comp.ForceProfile = data.ForceProfile;
			comp.CoilRiseTime = data.CoilRiseTime;
			comp.CoilFallTime = data.CoilFallTime;
			comp.PoleRadius = data.PoleRadius;
			comp.GrabBall = data.GrabBall;
			comp.GrabRadius = data.GrabRadius;
			comp.CylinderRadius = data.CylinderRadius;
			comp.CylinderHeight = data.CylinderHeight;
			comp.CylindricalDamping = data.Version >= 1 ? data.CylindricalDamping : MagnetComponent.DefaultCylindricalDamping;
			comp.GenerateCylinderCollider = data.Version >= 2 && data.GenerateCylinderCollider;
			comp.HeightRange = data.HeightRange;
			comp.IsEnabledOnStart = data.IsEnabledOnStart;
			comp.IsKinematic = data.IsKinematic;
			comp.DrawDebugForces = data.DrawDebugForces;
		}
	}
}
