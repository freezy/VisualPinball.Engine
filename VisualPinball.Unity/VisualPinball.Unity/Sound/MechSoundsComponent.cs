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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
		private List<MechSound> _sounds = new();

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		private ISoundEmitter _soundEmitter;
		private CancellationTokenSource tcs;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private void OnEnable()
		{
			_soundEmitter = GetComponent<ISoundEmitter>();
			_soundEmitter.OnSound += HandleSoundEmitterOnSound;
			tcs = new();
		}

		private void OnDisable()
		{
			if (_soundEmitter != null) {
				_soundEmitter.OnSound -= HandleSoundEmitterOnSound;
			}
			tcs.Cancel();
			tcs.Dispose();
			tcs = null;
		}

		// Async void is ok here because it's an event callback
		private async void HandleSoundEmitterOnSound(object sender, SoundEventArgs e)
		{
			List<Task> playTasks = new();
			foreach (MechSound sound in _sounds.Where(s => s.TriggerId == e.TriggerId))
				playTasks.Add(Play(sound, tcs.Token));
			await Task.WhenAll(playTasks);
		}

		private async Task Play(MechSound sound, CancellationToken ct)
		{
			AudioSource audioSource = null;
			CancellationTokenSource fadeCts = null;

			try {
				audioSource = gameObject.AddComponent<AudioSource>();
				sound.Sound.ConfigureAudioSource(audioSource, sound.Volume);
				audioSource.Play();
				_soundEmitter.OnSound += SoundEmitter_OnSound;
				if (sound.Sound.Loop && sound.Sound.FadeInTime > 0f) {
					fadeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
					await SoundUtils.Fade(audioSource, 0f, audioSource.volume, sound.Sound.FadeInTime, fadeCts.Token);
				}
				while (!ct.IsCancellationRequested && audioSource.isPlaying)
					await Task.Yield();
			} finally {
				_soundEmitter.OnSound -= SoundEmitter_OnSound;
				fadeCts?.Dispose();
				if (audioSource != null)
					Destroy(audioSource);
			}

			async void SoundEmitter_OnSound(object sender, SoundEventArgs eventArgs)
			{
				if (sound.HasStopTrigger && eventArgs.TriggerId == sound.StopTriggerId) {
					_soundEmitter.OnSound -= SoundEmitter_OnSound;
					fadeCts?.Cancel();
					if (sound.Sound.Loop && sound.Sound.FadeOutTime > 0f)
						await SoundUtils.Fade(audioSource, audioSource.volume, 0f, sound.Sound.FadeOutTime, ct);
					if (audioSource != null)
						audioSource.Stop();
				}
			}
		}
	}
}

