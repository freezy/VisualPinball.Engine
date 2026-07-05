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
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct TurntableState
	{
		internal float2 Position;
		internal float Height;
		internal float Radius;
		internal float HeightRange;
		internal float Speed;
		internal float TargetSpeed;
		internal float MaxSpeed;
		internal float SpinUp;
		internal float SpinDown;
		[MarshalAs(UnmanagedType.U1)]
		internal bool MotorOn;
		[MarshalAs(UnmanagedType.U1)]
		internal bool SpinClockwise;
		[MarshalAs(UnmanagedType.U1)]
		internal bool IsKinematic;
		internal float RotationAngle;
		internal float VisualSpeedFactor;
	}
}
