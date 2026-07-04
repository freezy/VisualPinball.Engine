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

namespace VisualPinball.Unity
{
	public readonly struct NudgeTelemetry
	{
		public readonly float2 CabinetAcceleration;
		public readonly float2 CabinetOffset;
		public readonly int ActiveSourceIndex;
		public readonly float3 PlumbPosition;
		public readonly float PlumbTiltPercent;
		public readonly int PlumbTiltIndex;
		public readonly bool PlumbTiltHigh;

		public NudgeTelemetry(float2 cabinetAcceleration, float2 cabinetOffset, int activeSourceIndex,
			float3 plumbPosition, float plumbTiltPercent, int plumbTiltIndex, bool plumbTiltHigh)
		{
			CabinetAcceleration = cabinetAcceleration;
			CabinetOffset = cabinetOffset;
			ActiveSourceIndex = activeSourceIndex;
			PlumbPosition = plumbPosition;
			PlumbTiltPercent = plumbTiltPercent;
			PlumbTiltIndex = plumbTiltIndex;
			PlumbTiltHigh = plumbTiltHigh;
		}
	}
}
