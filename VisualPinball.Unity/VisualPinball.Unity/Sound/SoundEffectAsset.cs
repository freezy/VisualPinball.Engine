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

// ReSharper disable InconsistentNaming

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Newtonsoft.Json;
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
		menuName = "Pinball/Sound/SoundEffectAsset",
		order = 102
	)]
	[PackAs("SoundEffectAsset")]
	[PackWith(typeof(SoundAssetPacker))]
	public class SoundEffectAsset : SoundAsset
	{
		public override bool Loop => _loop;
		[JsonIgnore] public SoundEffectType Type => _type;

		[SerializeField, JsonProperty]
		private Vector2 _volumeRange = new(1f, 1f);

		[SerializeField, JsonProperty]
		private Vector2 _pitchRange = new(1f, 1f);

		[SerializeField, JsonProperty]
		private bool _quadraticPitchVariation;

		[SerializeField, JsonProperty]
		private SoundEffectType _type;

		[SerializeField, JsonProperty]
		private bool _loop;

		[SerializeField, JsonProperty, Range(0, 10f)]
		private float _fadeInTime;

		[SerializeField, JsonProperty, Range(0, 10f)]
		private float _fadeOutTime;

		[SerializeField, JsonProperty, Range(0, 0.2f)]
		private float _cooldown = 0.02f;

		private static readonly ConditionalWeakTable<GameObject, SoundPlaybackContext> PlaybackContexts = new();

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public override void ConfigureAudioSource(AudioSource audioSource)
		{
			base.ConfigureAudioSource(audioSource);
			audioSource.spatialBlend = _type == SoundEffectType.Mechanical ? 1f : 0f;
			audioSource.volume = UnityEngine.Random.Range(_volumeRange.x, _volumeRange.y);
			audioSource.pitch = UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y);
			if (_quadraticPitchVariation) {
				// VPX varies above and below normal pitch using two squared random draws.
				var high = UnityEngine.Random.value;
				var low = UnityEngine.Random.value;
				audioSource.pitch = 1f + (_pitchRange.y - 1f) * high * high - (1f - _pitchRange.x) * low * low;
			}
			audioSource.loop = _loop;
		}

		public async Task Play(
			GameObject audioObj,
			CancellationToken allowFadeOutCt,
			CancellationToken instantCt,
			float volume = 1f,
			SoundPlaybackContext context = null,
			SoundEffectPlayback playback = default
		)
		{
			context ??= PlaybackContexts.GetOrCreateValue(audioObj);
			if (!playback.BypassCooldown && !context.TryStart(this, Time.unscaledTime, _cooldown)) return;

			GameObject voice = null;
			if (playback.Position.HasValue) {
				voice = new GameObject("Impact: " + name);
				SceneManager.MoveGameObjectToScene(voice, audioObj.scene);
				voice.transform.position = playback.Position.Value;
			}
			var audioSource = (voice ? voice : audioObj).AddComponent<AudioSource>();

			try
			{
				ConfigureAudioSource(audioSource);
				if (playback.Clip) audioSource.clip = playback.Clip;
				if (playback.Pitch > 0) audioSource.pitch *= playback.Pitch;
				bool mechanical = Application.isPlaying && _type == SoundEffectType.Mechanical;
#if UNITY_EDITOR
				mechanical &= !EditorSceneManager.IsPreviewScene(audioObj.scene);
#endif
				if (mechanical) MechanicalSoundGain.Register(audioSource);
				MechanicalSoundGain.SetAuthoredLevel(audioSource, MechanicalSoundGain.AuthoredLevel(audioSource) * volume);
				SoundPlayback.Play(audioSource, this, audioObj);
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
							MechanicalSoundGain.AuthoredLevel(audioSource),
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
							MechanicalSoundGain.AuthoredLevel(audioSource),
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
				MechanicalSoundGain.Unregister(audioSource);
				SoundPlayback.Stop(audioSource);
				var disposable = voice ? (UnityEngine.Object)voice : audioSource;
				if (disposable != null) {
					if (Application.isPlaying) Destroy(disposable);
					else DestroyImmediate(disposable);
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
				MechanicalSoundGain.SetAuthoredLevel(audioSource, Mathf.Lerp(fromVolume, toVolume, progress));
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
