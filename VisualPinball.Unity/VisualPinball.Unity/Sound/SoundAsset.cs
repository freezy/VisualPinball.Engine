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

// ReSharper disable InconsistentNaming

using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Represents a reusable collection of similar sounds, for example different samples of a
	/// flipper mechanism getting triggered. Supports multiple techniques to introduce variation
	/// for frequently used sounds. Instances of this class can be stored in the project files or
	/// in an asset library.
	/// </summary>
	[PackAs("SoundAsset")]
	[CreateAssetMenu(fileName = "Sound", menuName = "Visual Pinball/Sound", order = 102)]
	public class SoundAsset : ScriptableObject
	{
		public enum SelectionMethod
		{
			RoundRobin,
			Random
		}

		[FormerlySerializedAs("_description")]
		public string Description;

		[JsonIgnore]
		[FormerlySerializedAs("_clips")]
		public AudioClip[] Clips;

		[FormerlySerializedAs("_clipSelectionMethod")]
		public SelectionMethod ClipSelectionMethod;

		[FormerlySerializedAs("_volumeRange")]
		public Vector2 VolumeRange = new(1f, 1f);

		[FormerlySerializedAs("_pitchRange")]
		public Vector2 PitchRange = new(1f, 1f);

		[FormerlySerializedAs("_loop")]
		public bool Loop;

		[FormerlySerializedAs("_fadeInTime")]
		[SerializeField, Range(0, 10f)]
		public float FadeInTime;

		[FormerlySerializedAs("_fadeOutTime")]
		[SerializeField, Range(0, 10f)]
		public float FadeOutTime;

		[FormerlySerializedAs("_isSpatial")]
		[Tooltip("Should the sound appear to come from the position of the emitter?")]
		public bool IsSpatial = true;

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		private int _roundRobinIndex = 0;

		public void ConfigureAudioSource(AudioSource audioSource, float volume = 1)
		{
			audioSource.volume = volume * Random.Range(VolumeRange.x, VolumeRange.y);
			audioSource.pitch = Random.Range(PitchRange.x, PitchRange.y);
			audioSource.loop = Loop;
			audioSource.clip = GetClip();
			audioSource.spatialBlend = IsSpatial ? 0f : 1f;
			audioSource.outputAudioMixerGroup = _audioMixerGroup;
		}

		public bool IsValid()
		{
			if (Clips == null) {
				return false;
			}

			foreach (var clip in Clips) {
				if (clip != null) {
					return true;
				}
			}

			return false;
		}

		private AudioClip GetClip()
		{
			if (Clips.Length == 0) {
				throw new InvalidOperationException($"The sound asset '{name}' has no audio clips to play.");
			}

			switch (ClipSelectionMethod) {

				case SelectionMethod.RoundRobin:
					_roundRobinIndex %= Clips.Length;
					var clip = Clips[_roundRobinIndex];
					_roundRobinIndex++;
					return clip;

				case SelectionMethod.Random:
					return Clips[Random.Range(0, Clips.Length)];

				default:
					throw new NotImplementedException("Selection method not implemented.");
			}
		}
	}
}
