// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct FlipperStaticData : IComponentData
	{
		public float Inertia;
		public float AngleStart;
		public float AngleEnd;
		public float Strength;
		public float ReturnRatio;
		public float TorqueDamping;
		public float TorqueDampingAngle;
		public float RampUpSpeed;

		// only used in hit, probably split
		public float EndRadius;
		public float FlipperRadius;

	}
}
