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
	public interface IAnimationValueEmitter
	{
	}

	/// <summary>
	/// Components implementing this interface will get animation updates from the physics
	/// engine, and can either relay them to their children or perform their own transformations.
	/// </summary>
	public interface IAnimationValueEmitter<T> : IAnimationValueEmitter
	{
		/// <summary>
		/// Called by the physics engine to update the animation value of this component.
		/// </summary>
		/// <param name="value">Value passed to the component that animates.</param>
		void UpdateAnimationValue(T value);

		/// <summary>
		/// Event to notify potential children about angle changes. Only triggers when
		/// the angle actually changes, not on every update. The angle is in radians.
		/// </summary>
		public event Action<T> OnAnimationValueChanged;
	}
}
