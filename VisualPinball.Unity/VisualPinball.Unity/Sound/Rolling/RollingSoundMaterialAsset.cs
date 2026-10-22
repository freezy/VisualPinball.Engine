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

// ReSharper disable InconsistentNaming

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	public enum RollingSpeedMode { Tangential, IntegerHorizontal }
	public enum RollingPitchMode { Ratio, QuadraticHertz, LinearHertz }
	public enum RollingVariantMode { Random, FirstFreeSlot }
	public enum RollingSoundGroup { Playfield, Ramp }

	[Serializable]
	public sealed class RollingSoundResponse
	{
		[Newtonsoft.Json.JsonIgnore] public BallSoundMaterialAsset BallMaterial;
		[Newtonsoft.Json.JsonIgnore] public SoundEffectAsset Loop;
		[Newtonsoft.Json.JsonIgnore] public SoundEffectAsset Enter;
		[Newtonsoft.Json.JsonIgnore] public SoundEffectAsset Exit;
		public RollingSpeedMode SpeedMode;
		public float MinimumSpeed = 2;
		public float GainFactor = 0.00011f;
		public float GainExponent = 3;
		public RollingPitchMode PitchMode;
		public float PitchOffset;
		public float PitchFactor = 1;
		public float PitchOrigin;
		public float AttackSeconds = 0.01f;
		public float ReleaseSeconds = 0.02f;
		public float CrossfadeSeconds = 0.02f;
		public float TransitionMinimumSpeed = 2;
		public float TransitionCooldownSeconds = 0.05f;
		public bool AirArrival;
		public bool AirDeparture;

		public float Speed(global::Unity.Mathematics.float3 relativeVelocity, float tangentSpeed)
			=> SpeedMode == RollingSpeedMode.IntegerHorizontal
				? global::Unity.Mathematics.math.floor(global::Unity.Mathematics.math.length(relativeVelocity.xy)) : tangentSpeed;

		public float Gain(float speed) => speed < MinimumSpeed ? 0 : GainFactor * global::Unity.Mathematics.math.pow(speed, GainExponent);

		/// <summary>Unclamped VPX frequency offset, expressed as Unity's playback ratio.</summary>
		public float Pitch(float speed, int frequency)
		{
			if (PitchMode == RollingPitchMode.Ratio) return PitchFactor;
			var hz = PitchOffset + (PitchMode == RollingPitchMode.QuadraticHertz
				? PitchFactor * speed * speed : PitchFactor * (speed - PitchOrigin));
			return 1 + hz / System.Math.Max(1, frequency);
		}
	}

	/// <summary>A supporting surface's response for each ball material. Missing rows are silent.</summary>
	[CreateAssetMenu(menuName = "Pinball/Sound/Rolling Sound Material")]
	public sealed class RollingSoundMaterialAsset : ScriptableObject
	{
		public RollingSoundGroup Group;
		public RollingVariantMode VariantMode;
		[Tooltip("Surfaces sharing this key share their first-free voice slots (for example plastic and wire ramps).")]
		public string SlotPool;
		[Min(1)] public int SlotCount = 6;
		public BallSoundMaterialAsset FallbackBallMaterial;
		public RollingSoundResponse[] Responses = Array.Empty<RollingSoundResponse>();

		public RollingSoundResponse FindResponse(BallSoundMaterialAsset ballMaterial)
		{
			foreach (var response in Responses) if (response != null && response.BallMaterial == ballMaterial) return response;
			if (FallbackBallMaterial && FallbackBallMaterial != ballMaterial) {
				foreach (var response in Responses) if (response != null && response.BallMaterial == FallbackBallMaterial) return response;
			}
			return null;
		}
	}
}
