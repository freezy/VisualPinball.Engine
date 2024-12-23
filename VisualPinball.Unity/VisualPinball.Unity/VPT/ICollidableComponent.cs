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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public interface ICollidableComponent
	{
		/// <summary>
		/// Generates the colliders.
		/// </summary>
		/// <param name="player"></param>
		/// <param name="physicsEngine"></param>
		/// <param name="colliders"></param>
		/// <param name="kinematicColliders"></param>
		/// <param name="translateWithinPlayfieldMatrix"></param>
		/// <param name="margin"></param>
		internal void GetColliders(Player player, PhysicsEngine physicsEngine, ref ColliderReference colliders,
			ref ColliderReference kinematicColliders, float4x4 translateWithinPlayfieldMatrix, float margin);

		/// <summary>
		/// The unique identifier of the main item.
		/// </summary>
		public int ItemId { get; }

		/// <summary>
		/// Returns whether this specific item is set to collidable, i.e. whether can it ever be
		/// collided with during gameplay.
		/// </summary>
		internal bool IsCollidable { get; }

		/// <summary>
		/// The translation matrix, that will be applied in reverse to the ball
		/// for hit testing and collision.
		/// </summary>
		/// <param name="worldToPlayfield">The playfield's worldToLocal matrix.</param>
		/// <returns></returns>
		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield);
	}
}
