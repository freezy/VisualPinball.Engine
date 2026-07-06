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

using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

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
		[MarshalAs(UnmanagedType.U1)]
		internal bool IsEnabled;
		[MarshalAs(UnmanagedType.U1)]
		internal bool IsKinematic;
		internal MagnetForceProfile Profile;
		internal float HeightRange;
		internal MagnetType MagnetType;
		internal BitField64 GrabbedBalls;
		internal BitField64 ReleasedBalls;
	}
}
