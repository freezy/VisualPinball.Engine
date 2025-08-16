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
	/// <summary>
	/// Components implementing this interface will get animation updates from the physics
	/// engine, and can either relay them to their children or perform their own transformations.
	/// </summary>
	public interface IAnimationValueSource
	{
		/// <summary>
		/// Called by the physics engine to update the animatino value of this component.
		/// </summary>
		/// <param name="angleRad"></param>
		/// <param name="type"></param>
		void UpdateAnimationValue(float angleRad, AnimationValueType type);

		/// <summary>
		/// Event to notify potential children about angle changes. Only triggers when
		/// the angle actually changes, not on every update. The angle is in radians.
		/// </summary>
		public event Action<AnimationValue> OnAnimationValueChanged;
	}

	public readonly struct AnimationValue
	{
		public readonly float Value;
		public readonly AnimationValueType Type;

		public AnimationValue(float value, AnimationValueType type)
		{
			Value = value;
			Type = type;
		}
	}

	public enum AnimationValueType
	{
		Angle, YPosition
	}
}
