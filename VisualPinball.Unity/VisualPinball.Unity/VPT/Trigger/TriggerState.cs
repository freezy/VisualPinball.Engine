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
	internal struct TriggerState : IDisposable
	{
		internal readonly int ItemId;
		internal readonly int AnimatedItemId;
		internal TriggerStaticState Static;
		internal TriggerMovementState Movement;
		internal TriggerAnimationState Animation;
		internal FlipperCorrectionState FlipperCorrection;

		/// <summary>
		/// Default trigger usage.
		/// </summary>
		public TriggerState(int itemId, int animatedItemId, TriggerStaticState @static, TriggerMovementState movement, TriggerAnimationState animation)
		{
			ItemId = itemId;
			AnimatedItemId = animatedItemId;
			Static = @static;
			Movement = movement;
			Animation = animation;
			FlipperCorrection = default;
		}

		/// <summary>
		/// Flipper correction usage.
		/// </summary>
		public TriggerState(int itemId, TriggerStaticState @static, FlipperCorrectionState flipperCorrection)
		{
			ItemId = itemId;
			AnimatedItemId = 0;
			Static = @static;
			Movement = default;
			Animation = default;
			FlipperCorrection = flipperCorrection;
		}

		public void Dispose()
		{
			FlipperCorrection.Dispose();
		}
	}
}
