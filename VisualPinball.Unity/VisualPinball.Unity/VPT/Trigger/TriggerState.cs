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

namespace VisualPinball.Unity
{
	internal struct TriggerState
	{
		internal readonly int ItemId;
		internal readonly int AnimatedItemId;
		internal TriggerStaticData Static;
		internal TriggerMovementData Movement;
		internal TriggerAnimationData Animation;

		public TriggerState(int itemId, int animatedItemId, TriggerStaticData @static, TriggerMovementData movement, TriggerAnimationData animation)
		{
			ItemId = itemId;
			AnimatedItemId = animatedItemId;
			Static = @static;
			Movement = movement;
			Animation = animation;
		}
	}
}
