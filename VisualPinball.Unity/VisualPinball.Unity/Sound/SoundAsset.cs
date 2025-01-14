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
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Represents a reusable collection of similar sounds, for example different samples of a
	/// flipper mechanism getting triggered. Supports multiple techniques to introduce variation
	/// for frequently used sounds. Instances of this class can be stored in the project files or
	/// in an asset library.
	/// </summary>
	[CreateAssetMenu(fileName = "Sound", menuName = "Visual Pinball/Sound", order = 102)]
	public class SoundAsset : ScriptableObject
	{
		private enum SelectionMethod
		{
			RoundRobin,
			Random
		}

		[SerializeField]
		private string _description;

		[SerializeField]
		private AudioClip[] _clips;

		[SerializeField]
		private SelectionMethod _clipSelectionMethod;

		[SerializeField]
		private Vector2 _volumeRange = new(1f, 1f);

		[SerializeField]
		private Vector2 _pitchRange = new(1f, 1f);

		[SerializeField]
		private bool _loop;
		public bool Loop => _loop;

		[SerializeField, Range(0, 10f)] private float _fadeInTime;
		public float FadeInTime => _fadeInTime;

		[SerializeField, Range(0, 10f)] private float _fadeOutTime;
		public float FadeOutTime => _fadeOutTime;

		[Tooltip("Should the sound appear to come from the position of the emitter?")]
		[SerializeField]
		private bool _isSpatial = true;

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		private int _roundRobinIndex = 0;

		public void ConfigureAudioSource(AudioSource audioSource, float volume = 1)
		{
			audioSource.volume = Random.Range(_volumeRange.x, _volumeRange.y);
			audioSource.volume *= volume;
			audioSource.pitch = Random.Range(_pitchRange.x, _pitchRange.y);
			audioSource.loop = _loop;
			audioSource.clip = GetClip();
			audioSource.spatialBlend = _isSpatial ? 0f : 1f;
			audioSource.outputAudioMixerGroup = _audioMixerGroup;
		}

		public bool IsValid()
		{
			if (_clips == null) {
				return false;
			}

			foreach (var clip in _clips) {
				if (clip != null) {
					return true;
				}
			}

			return false;
		}

		private AudioClip GetClip()
		{
			_clips.ToList().RemoveAll(clip => clip == null);
			if (_clips.Length == 0) {
				throw new InvalidOperationException($"The sound asset '{name}' has no audio clips to play.");
			}

			switch (_clipSelectionMethod) {

				case SelectionMethod.RoundRobin:
					_roundRobinIndex %= _clips.Length;
					var clip = _clips[_roundRobinIndex];
					_roundRobinIndex++;
					return clip;

				case SelectionMethod.Random:
					return _clips[Random.Range(0, _clips.Length)];

				default:
					throw new NotImplementedException("Selection method not implemented.");
			}
		}
	}
}
