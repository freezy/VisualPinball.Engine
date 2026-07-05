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

using Unity.Mathematics;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct MagnetState
	{
		internal float2 Position;
		internal float Height;
		internal float Radius;
		internal float Strength;
		internal float GrabRadius;
		internal float PlanarDamping;
		internal bool IsEnabled;
		internal MagnetForceProfile Profile;
		internal float HeightRange;
		internal BitField64 GrabbedBalls;
	}
}
