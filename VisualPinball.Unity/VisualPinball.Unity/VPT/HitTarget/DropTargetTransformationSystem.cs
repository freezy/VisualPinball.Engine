// Visual Pinball Engine
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

using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class DropTargetTransformationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker(nameof(DropTargetTransformationSystem));
		private Player _player;

		protected override void OnStartRunning()
		{
			base.OnStartRunning();
			_player = Object.FindObjectOfType<Player>();
		}

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.WithoutBurst().WithName("DropTargetTransformationJob").ForEach((Entity entity, in DropTargetAnimationData data) =>
			{
				marker.Begin();

				var localPos = _player.DropTargetTransforms[entity].localPosition;
				_player.DropTargetTransforms[entity].localPosition = new Vector3(
					localPos.x,
					Physics.ScaleToWorld(data.ZOffset),
					localPos.z
				);

				marker.End();

			}).Run();
		}
	}
}
