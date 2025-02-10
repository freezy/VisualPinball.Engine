// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace VisualPinball.Unity
{
	public class MusicPlayer : MonoBehaviour
	{
		public bool ShouldPlay { get; set; }
		public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
		public bool StartAtFullVolume { get; set; }
		public MusicAsset MusicAsset { get; private set; }

		private AudioSource _audioSource;
		private float _fadeDuration;

		public void Init(MusicAsset musicAsset, float fadeDuration)
		{
			MusicAsset = musicAsset;
			_fadeDuration = fadeDuration;
		}

		private void Start()
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.volume = 0f;
		}

		private void Update()
		{
			var canPlay = _audioSource.isActiveAndEnabled;
#if UNITY_EDITOR
			canPlay &= EditorApplication.isFocused;
#else
			canPlay &= Application.isFocused;
#endif
			if (ShouldPlay && canPlay && !_audioSource.isPlaying)
			{
				var oldVolume = _audioSource.volume;
				MusicAsset.ConfigureAudioSource(_audioSource);
				_audioSource.Play();
				_audioSource.volume = StartAtFullVolume ? MusicAsset.Volume : oldVolume;
			}
			else if (!ShouldPlay && _audioSource.isPlaying && _audioSource.volume == 0f)
			{
				_audioSource.Stop();
			}

			var targetVolume = ShouldPlay ? MusicAsset.Volume : 0f;
			if (_audioSource.volume != targetVolume)
			{
				if (_fadeDuration == 0f)
				{
					_audioSource.volume = targetVolume;
				}
				else
				{
					if (_audioSource.volume < targetVolume)
						_audioSource.volume += 1 / _fadeDuration * Time.deltaTime;
					else
						_audioSource.volume -= 1 / _fadeDuration * Time.deltaTime;
					_audioSource.volume = Mathf.Clamp(_audioSource.volume, 0f, MusicAsset.Volume);
				}
			}
		}

		private void OnDestroy()
		{
			if (_audioSource != null)
				Destroy(_audioSource);
		}
	}
}
