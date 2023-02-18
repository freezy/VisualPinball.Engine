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
using UnityEngine;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	[CreateAssetMenu(fileName = "Sound", menuName = "Visual Pinball/Sound", order = 102)]
	public class SoundAsset : ScriptableObject
	{

		#region Properties

		public string Name;
		public string Description;

		[Range(0, 1)]
		public float VolumeCorrection = 1; //audio clips in unity have a volume range of 0 to 1

		public AudioClip[] Clips;

		public enum Selection
		{
			RoundRobin,
			Random
		}

		public Selection ClipSelection;

		[Range(0, 0.3f)]
		public float RandomizePitch;

		// todo needs to go through the mixer
		// [Range(0, 0.3f)]
		// public float RandomizeSpeed;

		[Range(0, 0.5f)]
		public float RandomizeVolume;

		public bool Loop;

		#endregion

		#region Runtime

		private int _clipIndex;

		#endregion
		
		public void Play(AudioSource audioSource)
		{
			if (Clips.Length == 0) {
				return;
			}
			audioSource.volume = Volume;
			audioSource.pitch = Pitch;
			audioSource.loop = Loop;
			audioSource.clip = GetClip();
			audioSource.Play();
		}
		
		
		public void Stop(AudioSource audioSource)
		{
			audioSource.Stop();
		}

		private float Pitch => 1f + Random.Range(-RandomizePitch / 2, RandomizePitch / 2);
		private float Volume => VolumeCorrection - Random.Range(0, RandomizeVolume);

		private AudioClip GetClip()
		{
			switch (ClipSelection) {
				case Selection.RoundRobin:
					var clip = Clips[_clipIndex];
					_clipIndex = (_clipIndex + 1) % Clips.Length;
					return clip;
				
				case Selection.Random:
					return Clips[Random.Range(0, Clips.Length)];
				
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
