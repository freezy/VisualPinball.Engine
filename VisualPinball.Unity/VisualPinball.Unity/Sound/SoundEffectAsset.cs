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
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NLog.Logger;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace VisualPinball.Unity
{
	public enum SoundEffectType
	{
		Mechanical,
		Synthetic,
	}

	/// <summary>
	/// Represents a reusable collection of similar sounds, for example different samples of a
	/// flipper mechanism getting triggered. Supports randomizing pitch and volume to introduce
	/// variation. Supports looping sounds with optional fade.
	/// </summary>
	[CreateAssetMenu(
		fileName = "SoundEffect",
		menuName = "Visual Pinball/Sound/SoundEffectAsset",
		order = 102
	)]
	public class SoundEffectAsset : SoundAsset
	{
		public override bool Loop => _loop;

		[SerializeField]
		private Vector2 _volumeRange = new(1f, 1f);

		[SerializeField]
		private Vector2 _pitchRange = new(1f, 1f);

		[SerializeField]
		private SoundEffectType _type;

		[SerializeField]
		private bool _loop;

		[SerializeField, Range(0, 10f)]
		private float _fadeInTime;

		[SerializeField, Range(0, 10f)]
		private float _fadeOutTime;

		[SerializeField, Range(0, 0.2f)]
		private float _cooldown = 0.02f;

		[NonSerialized]
		private float _lastPlayStartTime = -1f;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override void ConfigureAudioSource(AudioSource audioSource)
		{
			base.ConfigureAudioSource(audioSource);
			audioSource.spatialBlend = _type == SoundEffectType.Mechanical ? 1f : 0f;
			audioSource.volume = UnityEngine.Random.Range(_volumeRange.x, _volumeRange.y);
			audioSource.pitch = UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y);
			audioSource.loop = _loop;
		}

		public async Task Play(
			GameObject audioObj,
			CancellationToken allowFadeOutCt,
			CancellationToken instantCt,
			float volume = 1f
		)
		{
			float timeSinceLastPlay = Time.unscaledTime - _lastPlayStartTime;
			if (timeSinceLastPlay < _cooldown)
			{
				Logger.Warn(
					$"Will not play sound effect '{name}' because the time since last play is "
						+ $"{timeSinceLastPlay} seconds, which is less than the cooldown of "
						+ $"{_cooldown} seconds. If this is not intended by the table author, they "
						+ $"should lower the '{nameof(_cooldown)}' parameter in the sound effect "
						+ "asset inspector."
				);
				return;
			}
			_lastPlayStartTime = Time.unscaledTime;

			var audioSource = audioObj.AddComponent<AudioSource>();

			try
			{
				ConfigureAudioSource(audioSource);
				audioSource.volume *= volume;
				audioSource.Play();
				using var eitherCts = CancellationTokenSource.CreateLinkedTokenSource(
					allowFadeOutCt,
					instantCt
				);

				// Fade in
				if (_loop && _fadeInTime > 0f)
				{
					try
					{
						await FadeOrFinish(
							audioSource,
							0f,
							audioSource.volume,
							_fadeInTime,
							eitherCts.Token
						);
					}
					catch (OperationCanceledException)
					{
						instantCt.ThrowIfCancellationRequested();
					}
				}

				// Play until sound stops or cancellation is requested
				try
				{
					await WaitUntilAudioStops(audioSource, eitherCts.Token);
				}
				catch (OperationCanceledException)
				{
					instantCt.ThrowIfCancellationRequested();
					// Fade out
					if (_loop && _fadeOutTime > 0f)
					{
						await FadeOrFinish(
							audioSource,
							audioSource.volume,
							0f,
							_fadeOutTime,
							instantCt
						);
					}
					allowFadeOutCt.ThrowIfCancellationRequested();
				}
			}
			finally
			{
				if (audioSource != null)
				{
					if (Application.isPlaying)
						Destroy(audioSource);
					else
						DestroyImmediate(audioSource);
				}
			}
		}

		public static async Task FadeOrFinish(
			AudioSource audioSource,
			float fromVolume,
			float toVolume,
			float duration,
			CancellationToken ct
		)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			var waitUntilStopTask = WaitUntilAudioStops(audioSource, cts.Token);
			var fadeTask = Fade(audioSource, fromVolume, toVolume, duration, cts.Token);

			var completedTask = await Task.WhenAny(waitUntilStopTask, fadeTask);
			cts.Cancel();
			try
			{
				await Task.WhenAll(waitUntilStopTask, fadeTask);
			}
			catch (OperationCanceledException)
			{
				ct.ThrowIfCancellationRequested();
			}
		}

		public static async Task Fade(
			AudioSource audioSource,
			float fromVolume,
			float toVolume,
			float duration,
			CancellationToken ct
		)
		{
			float progress = 0f;
#if UNITY_EDITOR
			// Time.deltaTime doesn't really work in the editor outside play mode
			var lastTime = EditorApplication.timeSinceStartup;
#endif
			while (progress < 1f && audioSource != null)
			{
				ct.ThrowIfCancellationRequested();
				audioSource.volume = Mathf.Lerp(fromVolume, toVolume, progress);
#if UNITY_EDITOR
				var deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
				lastTime = EditorApplication.timeSinceStartup;
#else
				var deltaTime = Time.deltaTime;
#endif
				progress += 1f / duration * deltaTime;
				await Task.Yield();
			}
		}

#if UNITY_EDITOR
		public async Task PlayInEditorPreviewScene(
			CancellationToken allowFadeOutCt,
			CancellationToken instantCt
		)
		{
			Scene previewScene = EditorSceneManager.NewPreviewScene();
			try
			{
				previewScene.name = "VPE Audio Preview";
				var audioObj = new GameObject("Audio Preview");
				SceneManager.MoveGameObjectToScene(audioObj, previewScene);
				await Play(audioObj, allowFadeOutCt, instantCt);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(previewScene);
			}
		}
#endif
	}
}
