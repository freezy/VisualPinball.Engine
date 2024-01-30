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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using UnityEngine.Audio;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sounds/Mechanical Sounds")]
	public class MechSoundsComponent : MonoBehaviour
	{
		[SerializeField]
		public List<MechSound> Sounds = new();

		[SerializeField]
		[Tooltip("If left blank, looks for an Audio Mixer in closest parent up the hierarchy.")]
		public AudioMixerGroup AudioMixer;

		[NonSerialized]
		private ISoundEmitter _soundEmitter;
		[NonSerialized]
		private SerializableDictionary<SoundAsset, AudioSource> _audioSources = new SerializableDictionary<SoundAsset, AudioSource>(); 
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private Coroutine _co;

		private void Awake()
		{
			_soundEmitter = GetComponent<ISoundEmitter>();
		}

		private void Start()
		{
			if (AudioMixer == null)
			{
				// find an Audio Mixer by searching up the hierarchy
				AudioSource audioSource = GetComponentInParent<AudioSource>();
				if (audioSource != null)
				{
					AudioMixer = audioSource.outputAudioMixerGroup;
				}
				else
				{
					Logger.Warn($"Sounds will not play without an Audio Mixer.");
				}
			}

			if (_soundEmitter != null) {
				_soundEmitter.OnSound += EmitSound;

			} else {
				// ? is AudioSource really a dependency here??
				Logger.Warn($"Cannot initialize mech sound for {name} due to missing ISoundEmitter or AudioSource.");
			}
		}

		private void OnDestroy()
		{
			if (_soundEmitter != null) {
				_soundEmitter.OnSound -= EmitSound;
			}
		}

		private void EmitSound(object sender, SoundEventArgs e)
		{
			int clipCount = 0;

			foreach(MechSound sound in Sounds)
			{
				// filter for the TriggerId
				if (sound.TriggerId != e.TriggerId) continue;

				// get or create the AudioSource
				AudioSource audioSource;
				if (_audioSources.ContainsKey(sound.Sound))
				{
					audioSource = _audioSources[sound.Sound];
				}
				else
				{
					audioSource = gameObject.AddComponent<AudioSource>();
					audioSource.outputAudioMixerGroup = AudioMixer;
					_audioSources.Add(sound.Sound, audioSource);
				}

				if (sound.Action == MechSoundAction.Stop)
				{
					sound.Sound.Stop(audioSource);
					Debug.Log($"Stopping sound {e.TriggerId} for {name}");
					// we're done
					continue;
				}

				// else sound.Action == MechSoundAction.Play
				float fade = sound.Fade;
				bool fadeVolume = false;

				//convert fade duration from milliseconds to seconds for use with StartFade method
				if (fade > 0)
				{
					//dont have a fade minimum of less than 1 second, if there is a fade. Less than 1 second fade,
					//and the underlying method 'FadeMixerGroup.StartFade' will break down and not work correctly
					if (fade < 1000)
					{ fade = 1000; }

				    fade = fade / 1000;
					fadeVolume = true;
				}

				float volume = e.Volume;

				AudioMixer audioMixer = GetComponent<AudioSource>().outputAudioMixerGroup.audioMixer;
				sound.Sound.Play(audioSource, volume);

				/* set audio mixer volume to decibel equivalent of volume slider value
				   mixer volume is set at 0 dB when added to audiosource
				   volume of 1 in slider is equivalent to 0 dB
				*/
				string exposedParameter = "vol1";
				float sliderDBVolume = Mathf.Log10(volume) * 20;
				float mixerVolume;
				
				audioMixer.GetFloat(exposedParameter, out mixerVolume);

				//current coroutine is still fading the audio clip and needs to be stopped and volume reset
				if (mixerVolume < sliderDBVolume)
				{
					StopCoroutine(_co);
					audioMixer.SetFloat(exposedParameter, sliderDBVolume);
				}

				if (fadeVolume)
				{ _co = StartCoroutine(FadeMixerGroup.StartFade(audioMixer, exposedParameter, 1, 0)); }
				

				Debug.Log($"Playing sound {e.TriggerId} for {name}");
			}
		}
	}
}

