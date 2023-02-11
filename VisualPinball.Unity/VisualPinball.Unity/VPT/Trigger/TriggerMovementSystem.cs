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
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal partial class TriggerMovementSystem : SystemBaseStub
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("TriggerMovementSystem");

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

			// fixme job
			// Entities.WithoutBurst().WithName("TriggerMovementJob").ForEach((Entity entity, in TriggerMovementData data) => {
			//
			// 	marker.Begin();
			// 	
			// 	var transform = _player.TriggerTransforms[entity];
			// 	if (!_initialOffset.ContainsKey(entity)) {
			// 		_initialOffset[entity] = transform.position.y;
			// 	}
			//
			// 	var worldPos = transform.position;
			// 	worldPos.y = _initialOffset[entity] + VisualPinball.Unity.Physics.ScaleToWorld(data.HeightOffset);
			// 	transform.position = worldPos;
			//
			// 	marker.End();
			//
			// }).Run();
		}
	}
}
