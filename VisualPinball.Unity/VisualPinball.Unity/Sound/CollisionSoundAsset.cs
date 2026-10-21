// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum ImpactSoundMetric { Speed, PlanarSpeed, PlanarSpeedSquared, VerticalSpeed, VerticalSpeedSquared, Constant, SpeedSquared }

	[Serializable]
	public class ImpactSoundRule
	{
		public string Description;
		public ImpactSoundMetric Selection = ImpactSoundMetric.PlanarSpeed;
		public float Minimum;
		public float Maximum = float.MaxValue;
		public bool IncludeMinimum = true;
		public bool IncludeMaximum = true;
		public ImpactSoundMetric Volume = ImpactSoundMetric.PlanarSpeedSquared;
		public float Gain = 1;
		public int[] Clips = Array.Empty<int>();
		public float[] ClipGains = Array.Empty<float>();
		public bool PitchFromSpeed;
		public Vector2 IncomingX = new(-float.MaxValue, float.MaxValue);
		public Vector2 IncomingY = new(-float.MaxValue, float.MaxValue);
		public bool InvertDirection;

		public bool Matches(in BallImpact impact)
		{
			var speed = Measure(Selection, impact);
			var direction = impact.IncomingVelocity.x >= IncomingX.x && impact.IncomingVelocity.x < IncomingX.y
				&& impact.IncomingVelocity.y > IncomingY.x && impact.IncomingVelocity.y <= IncomingY.y;
			return (IncludeMinimum ? speed >= Minimum : speed > Minimum)
				&& (IncludeMaximum ? speed <= Maximum : speed < Maximum)
				&& (InvertDirection ? !direction : direction);
		}

		public static float Measure(ImpactSoundMetric metric, in BallImpact impact)
		{
			var planar = math.length(impact.Velocity.xy);
			return metric switch {
				ImpactSoundMetric.Speed => impact.Speed,
				ImpactSoundMetric.SpeedSquared => impact.Speed * impact.Speed,
				ImpactSoundMetric.PlanarSpeed => planar,
				// VP's Vol() squares its integer BallVel(), while its band selection uses raw speed.
				ImpactSoundMetric.PlanarSpeedSquared => math.floor(planar) * math.floor(planar),
				ImpactSoundMetric.VerticalSpeed => math.abs(impact.IncomingVelocity.z),
				ImpactSoundMetric.VerticalSpeedSquared => impact.IncomingVelocity.z * impact.IncomingVelocity.z,
				_ => 1
			};
		}
	}

	/// <summary>Reusable impact rules and their flattened sample bank. Clip indices keep packaging self-contained.</summary>
	[CreateAssetMenu(menuName = "Pinball/Sound/Collision Sound")]
	[PackAs("CollisionSoundAsset")]
	[PackWith(typeof(SoundAssetPacker))]
	public class CollisionSoundAsset : SoundEffectAsset
	{
		[JsonProperty] public ImpactSoundRule[] Rules = Array.Empty<ImpactSoundRule>();
		[JsonProperty, Min(0)] public uint RetriggerMsec = 10;
		[JsonProperty] public bool UpwardSurfaceOnly;

		public bool Accepts(in BallImpact impact) => !UpwardSurfaceOnly
			|| (impact.Normal.z > .5f && impact.IncomingVelocity.z < -1);
	}
}
