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
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinballUnity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal partial class BumperSkirtMovementSystem : SystemBaseStub
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperSkirtMovementSystem");

		private Player _player;

		protected override void OnStartRunning()
		{
			base.OnStartRunning();
			_player = Object.FindObjectOfType<Player>();
		}

		protected override void OnUpdate()
		{
			// fixme job
			// var marker = PerfMarker;
			// Entities.WithoutBurst().WithName("BumperSkirtMovementJob").ForEach((Entity entity, in BumperSkirtAnimationData data) => {
			//
			// 	marker.Begin();
			//
			// 	var transform = _player.BumperSkirtTransforms[entity];
			// 	var parentRotation = transform.parent.rotation;
			// 	transform.rotation = Quaternion.Euler(data.Rotation.x, 0, -data.Rotation.y) * parentRotation;
			//
			// 	marker.End();
			//
			// }).Run();
		}
	}
}
