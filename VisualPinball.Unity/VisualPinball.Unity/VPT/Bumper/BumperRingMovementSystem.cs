﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class BumperRingMovementSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BumperRingMovementSystem");

		private Player _player;

		private readonly Dictionary<Entity, float> _initialOffset = new();

		protected override void OnStartRunning()
		{
			base.OnStartRunning();
			_player = Object.FindObjectOfType<Player>();
		}

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithoutBurst().WithName("BumperRingMovementJob").ForEach((Entity entity, in BumperRingAnimationData data) => {

				marker.Begin();

				var transform = _player.BumperRingTransforms[entity];
				var worldPos = transform.TransformPoint(transform.localPosition);
				if (!_initialOffset.ContainsKey(entity)) {
					_initialOffset[entity] = worldPos.y;
				}

				var limit = data.DropOffset + data.HeightScale * 0.5f;
				var localLimit = _initialOffset[entity] + limit;
				var localOffset = localLimit / limit * data.Offset;
				
				worldPos.y = _initialOffset[entity] + Physics.ScaleToWorld(localOffset);
				transform.localPosition = transform.InverseTransformPoint(worldPos);

				marker.End();

			}).Run();
		}
	}
}
