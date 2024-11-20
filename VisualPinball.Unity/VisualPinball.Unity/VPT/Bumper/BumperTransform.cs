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

using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Applies the state to the scene, aka the transform of the game objects.
	/// </summary>
	internal static class BumperTransform
	{
		internal static readonly Dictionary<int, float> InitialOffset = new();

		internal static void UpdateRing(int itemId, in BumperRingAnimationState state, Transform transform)
		{
			var worldPos = transform.position;
			InitialOffset.TryAdd(itemId, worldPos.y);

			var limit = state.DropOffset + state.HeightScale * 0.5f;
			var localLimit = InitialOffset[itemId] + limit;
			var localOffset = localLimit / limit * state.Offset;

			worldPos.y = InitialOffset[itemId] + Physics.ScaleToWorld(localOffset);
			transform.position = worldPos;
		}

		internal static void UpdateSkirt(in BumperSkirtAnimationState state, Transform transform)
		{
			var parentRotation = transform.parent.rotation;
			transform.rotation = Quaternion.Euler(state.Rotation.x, 0, -state.Rotation.y) * parentRotation;
		}
	}
}
