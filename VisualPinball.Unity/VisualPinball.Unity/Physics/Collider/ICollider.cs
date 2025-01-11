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
	public interface ICollider
	{
		/// <summary>
		/// The bounds of the collider.
		/// </summary>
		ColliderBounds Bounds { get; }

		/// <summary>
		/// If true, the collider is fully transformable and can be moved, scaled and rotated freely.
		///
		/// Fully transformable colliders are relevant when the object is set to be a kinematic collider.
		/// If fully transformable, the collider will be transformed if the object's transformation matrix
		/// has changed. This is less expensive than projecting the ball into the collider's local space,
		/// which is the case for non fully transformable colliders.
		///
		/// If the collider is not kinematic but transformed in a way supported by the collider, the collider
		/// will simply be transformed without the ball projection. The problem with kinematic colliders is
		/// that we don't know in advance how the collider will be transformed, so we can't assume it'll be
		/// within the collider's capability to transform. Thus, only fully transformable colliders can be
		/// dynamically transformed without ball projection. This is what this flag is for.
		/// </summary>
		bool IsFullyTransformable { get; }
	}
}
