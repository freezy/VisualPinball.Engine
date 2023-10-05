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
		internal NativeParallelHashMap<int, BumperState> BumperStates;
		internal NativeParallelHashMap<int, DropTargetState> DropTargetStates;
		internal NativeParallelHashMap<int, FlipperState> FlipperStates;
		internal NativeParallelHashMap<int, GateState> GateStates;
		internal NativeParallelHashMap<int, HitTargetState> HitTargetStates;
		internal NativeParallelHashMap<int, KickerState> KickerStates;
		internal NativeParallelHashMap<int, PlungerState> PlungerStates;
		internal NativeParallelHashMap<int, SpinnerState> SpinnerStates;
		internal NativeParallelHashMap<int, SurfaceState> SurfaceStates;

		public PhysicsState(ref PhysicsEnv env, ref NativeOctree<int> octree, ref BlobAssetReference<ColliderBlob> colliders,
			ref NativeQueue<EventData>.ParallelWriter eventQueue, ref InsideOfs insideOfs, ref NativeParallelHashMap<int, BallData> balls,
			ref NativeParallelHashMap<int, BumperState> bumperStates, ref NativeParallelHashMap<int, DropTargetState> dropTargetStates,
			ref NativeParallelHashMap<int, FlipperState> flipperStates, ref NativeParallelHashMap<int, GateState> gateStates,
			ref NativeParallelHashMap<int, HitTargetState> hitTargetStates, ref NativeParallelHashMap<int, KickerState> kickerStates,
			ref NativeParallelHashMap<int, PlungerState> plungerStates, ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			ref NativeParallelHashMap<int, SurfaceState> surfaceStates)
		{
			Env = env;
			Octree = octree;
			Colliders = colliders;
			EventQueue = eventQueue;
			InsideOfs = insideOfs;
			Balls = balls;
			BumperStates = bumperStates;
			DropTargetStates = dropTargetStates;
			FlipperStates = flipperStates;
			GateStates = gateStates;
			HitTargetStates = hitTargetStates;
			KickerStates = kickerStates;
			PlungerStates = plungerStates;
			SpinnerStates = spinnerStates;
			SurfaceStates = surfaceStates;
		}

		internal Collider GetCollider(int colliderId) => Colliders.Value.Colliders[colliderId].Value;

		internal ref FlipperState GetFlipperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref FlipperStates.GetValueByRef(collider.ItemId);
		}

		internal ref PlungerState GetPlungerState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref PlungerStates.GetValueByRef(collider.ItemId);
		}

		internal bool HasDropTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return DropTargetStates.ContainsKey(collider.ItemId);
		}

		internal ref DropTargetState GetDropTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref DropTargetStates.GetValueByRef(collider.ItemId);
		}

		internal ref BumperState GetBumperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref BumperStates.GetValueByRef(collider.ItemId);
		}
	}
}
