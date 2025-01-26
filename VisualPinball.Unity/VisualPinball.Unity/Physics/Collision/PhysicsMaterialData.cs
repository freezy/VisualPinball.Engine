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

using System;

namespace VisualPinball.Unity
{
	public struct PhysicsMaterialData : IEquatable<PhysicsMaterialData>
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float ScatterAngleRad;
		public bool UseElasticityOverVelocity;
		public bool UseFrictionOverVelocity;

		public static bool operator ==(PhysicsMaterialData a, PhysicsMaterialData b) => a.Equals(b);
		public static bool operator !=(PhysicsMaterialData a, PhysicsMaterialData b) => !a.Equals(b);

		public readonly bool Equals(PhysicsMaterialData other)
		{
			return
				Elasticity == other.Elasticity &&
				ElasticityFalloff == other.ElasticityFalloff &&
				Friction == other.Friction &&
				ScatterAngleRad == other.ScatterAngleRad &&
				UseElasticityOverVelocity == other.UseElasticityOverVelocity &&
				UseFrictionOverVelocity == other.UseFrictionOverVelocity;
		}

		public override readonly bool Equals(object obj)
		{
			if (obj is PhysicsMaterialData other) {
				return Equals(other);
			}
			return false;
		}

		public override readonly int GetHashCode()
		{
			return HashCode.Combine(Elasticity, ElasticityFalloff, Friction, ScatterAngleRad, UseElasticityOverVelocity, UseFrictionOverVelocity);
		}
	}
}
