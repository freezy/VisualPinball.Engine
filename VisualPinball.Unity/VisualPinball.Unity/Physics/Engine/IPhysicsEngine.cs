﻿// Visual Pinball Engine
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

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A swappable engine that implements VPE's rigid body physics.
	/// </summary>
	public interface IPhysicsEngine : IEngine
	{
		/// <summary>
		/// Initialize and enable the engine.
		/// </summary>
		/// <remarks>
		/// All engines are instantiated, but they should only activate
		/// themselves when this method is called.
		/// </remarks>
		/// <param name="tableComponent"></param>
		/// <param name="ballManager"></param>
		void Init(TableComponent tableComponent, BallManager ballManager);

		/// <summary>
		/// Create a new ball and returns its entity.
		/// </summary>
		/// <param name="ballGo">Created game object of the ball</param>
		/// <param name="id">Unique ID of the ball</param>
		/// <param name="worldPos">Position in world space</param>
		/// <param name="localPos">Position in local space</param>
		/// <param name="localVel">Velocity in local space</param>
		/// <param name="scale">Scale relative to ball mesh</param>
		/// <param name="mass">Physics mass</param>
		/// <param name="radius">Radius in local space</param>
		/// <param name="kickerRef">If created within a kicker, this is the kicker entity</param>
		void BallCreate(GameObject ballGo, int id, in float3 worldPos, in float3 localPos, in float3 localVel,
			in float scale, in float mass, in float radius, in int kickerId);

		/// <summary>
		/// Rolls the ball manually to a position on the playfield.
		/// </summary>
		/// <param name="ballEntity">Ball entity</param>
		/// <param name="targetWorldPosition">New position in world space</param>
		void BallManualRoll(in Entity ballEntity, in float3 targetWorldPosition);

		/// <summary>
		/// Rotate the flipper "up" (button pressed)
		/// </summary>
		/// <param name="itemId"></param>
		void FlipperRotateToEnd(in int itemId);

		/// <summary>
		/// Rotate the flipper "down" (button released)
		/// </summary>
		/// <param name="itemId"></param>
		void FlipperRotateToStart(in int itemId);

		/// <summary>
		/// Returns a simplified version of all flipper states for the
		/// debug UI to deal with.
		/// </summary>
		/// <returns>All flipper states</returns>
		DebugFlipperState[] FlipperGetDebugStates();

		/// <summary>
		/// Returns which sliders for flippers the debug UI should display.
		/// </summary>
		DebugFlipperSlider[] FlipperGetDebugSliders();

		/// <summary>
		/// Updates a flipper property during gameplay.
		/// </summary>
		/// <param name="param">Which property to update</param>
		/// <param name="v">New value</param>
		void SetFlipperDebugValue(DebugFlipperSliderParam param, float v);

		/// <summary>
		/// Retrieves a flipper property during gameplay.
		/// </summary>
		/// <param name="param">Which property to retrieve</param>
		/// <returns>Value of the property</returns>
		float GetFlipperDebugValue(DebugFlipperSliderParam param);
	}
}
