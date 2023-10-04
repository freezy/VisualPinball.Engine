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
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;
using Physics = UnityEngine.Physics;

namespace VisualPinballUnity
{
	internal static class BumperTransformation
	{
		private static readonly Dictionary<int, float> InitialOffset = new();

		internal static void UpdateRing(int itemId, in BumperRingAnimationData data, Transform transform)
		{
			var worldPos = transform.position;
			InitialOffset.TryAdd(itemId, worldPos.y);

			var limit = data.DropOffset + data.HeightScale * 0.5f;
			var localLimit = InitialOffset[itemId] + limit;
			var localOffset = localLimit / limit * data.Offset;

			worldPos.y = InitialOffset[itemId] + VisualPinball.Unity.Physics.ScaleToWorld(localOffset);
			transform.position = worldPos;
		}

		internal static void UpdateSkirt(in BumperSkirtAnimationData data, Transform transform)
		{
			var parentRotation = transform.parent.rotation;
			transform.rotation = Quaternion.Euler(data.Rotation.x, 0, -data.Rotation.y) * parentRotation;
		}
	}
}
