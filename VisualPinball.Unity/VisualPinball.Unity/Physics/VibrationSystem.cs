// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using Unity.Transforms;
using UnityEngine;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class VibrationSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("VibrationSystem");

		public float Strength = 1f;
		public float Speed = 1f;
		public float Duration = 1f;

		private VisualPinballSimulationSystemGroup _simulationSystemGroup;
		private float4x4 _baseTransform;
		private EndSimulationEntityCommandBufferSystem _ecb;

		protected override void OnCreate()
		{
			_simulationSystemGroup = World.GetExistingSystem<VisualPinballSimulationSystemGroup>();
			_ecb = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		}

		protected override void OnStartRunning()
		{
			var root = Object.FindObjectOfType<TableAuthoring>();
			_baseTransform = root.gameObject.transform.localToWorldMatrix;
		}

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			var time = _simulationSystemGroup.TimeMsec;

			var ecb = _ecb.CreateCommandBuffer();
			var ltw = _baseTransform;

			var speed = Speed;
			var strength = Strength;
			var duration = Duration;

			Entities.WithName("VibrationJob").ForEach((Entity entity, ref Translation trans, ref VibrationData vibrationData) => {

				marker.Begin();

				var x = (time - vibrationData.HitTimeMs) / 1000f;
				if (x > duration) {
					ecb.RemoveComponent<VibrationData>(entity);

				} else {
					var localPos = math.transform(math.inverse(ltw), vibrationData.Position);
					var y = (math.pow(-(x / duration), 3) + 1) * math.cos(x * speed);

					var newDirection = localPos + vibrationData.HitNormal * (strength * y);
					trans.Value = math.transform(ltw, newDirection);
				}

				marker.End();

			}).Run();
		}
	}
}
