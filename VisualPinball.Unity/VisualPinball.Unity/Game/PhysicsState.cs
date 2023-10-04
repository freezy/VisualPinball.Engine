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

using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct PhysicsState
	{
		internal PhysicsEnv Env;
		internal NativeOctree<int> Octree;
		internal BlobAssetReference<ColliderBlob> Colliders;
		internal NativeQueue<EventData>.ParallelWriter EventQueue;
		internal InsideOfs InsideOfs;
		internal NativeParallelHashMap<int, BallData> Balls;
		internal NativeParallelHashMap<int, FlipperState> FlipperStates;
		internal NativeParallelHashMap<int, BumperState> BumperStates;

		public PhysicsState(ref PhysicsEnv env, ref NativeOctree<int> octree, ref BlobAssetReference<ColliderBlob> colliders,
			ref NativeQueue<EventData>.ParallelWriter eventQueue, ref InsideOfs insideOfs, ref NativeParallelHashMap<int, BallData> balls,
			ref NativeParallelHashMap<int, FlipperState> flipperStates, ref NativeParallelHashMap<int, BumperState> bumperStates)
		{
			Env = env;
			Octree = octree;
			Colliders = colliders;
			EventQueue = eventQueue;
			InsideOfs = insideOfs;
			Balls = balls;
			FlipperStates = flipperStates;
			BumperStates = bumperStates;
		}

		internal Collider GetCollider(int colliderId) => Colliders.Value.Colliders[colliderId].Value;

		internal ref FlipperState GetFlipperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref FlipperStates.GetValueByRef(collider.ItemId);
		}

		internal ref BumperState GetBumperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref BumperStates.GetValueByRef(collider.ItemId);
		}
	}
}
