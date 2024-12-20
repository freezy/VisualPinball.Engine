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

using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace VisualPinball.Unity
{
	public static class SoundUtils
	{
		public static async Task Fade(AudioSource audioSource, float fromVolume, float toVolume, float duration, CancellationToken ct)
		{
			float progress = 0f;
#if UNITY_EDITOR
			// Time.deltaTime doesn't really work in the editor
			var lastTime = EditorApplication.timeSinceStartup;
#endif
			while (progress < 1f) {
				ct.ThrowIfCancellationRequested();
				audioSource.volume = Mathf.Lerp(fromVolume, toVolume, progress);
#if UNITY_EDITOR
				var deltaTime = (float)(EditorApplication.timeSinceStartup - lastTime);
				lastTime = EditorApplication.timeSinceStartup;
#else
				var deltaTime = Time.deltaTime;
#endif
				progress += (1f / duration) * deltaTime;
				await Task.Yield();
			}
		}

#if UNITY_EDITOR
		public static async Task PlayInEditorPreviewScene(SoundAsset sound, CancellationToken allowFadeOutCt, CancellationToken instantCt)
		{
			Scene previewScene = EditorSceneManager.NewPreviewScene();
			try {
				previewScene.name = "VPE Audio Preview";
				var audioObj = new GameObject("Audio Preview");
				SceneManager.MoveGameObjectToScene(audioObj, previewScene);
				await Play(sound, audioObj, allowFadeOutCt, instantCt);
			} finally {
				EditorSceneManager.ClosePreviewScene(previewScene);
			}
		}
#endif

		public static async Task Play(SoundAsset sound, GameObject audioObj, CancellationToken allowFadeOutCt, CancellationToken instantCt, float volume = 1f)
		{
			var audioSource = audioObj.AddComponent<AudioSource>();

			try {
				sound.ConfigureAudioSource(audioSource);
				audioSource.volume *= volume;
				audioSource.Play();
				using var eitherCts = CancellationTokenSource.CreateLinkedTokenSource(allowFadeOutCt, instantCt);

				// Fade in
				if (sound.Loop && sound.FadeInTime > 0f) {
					try {
						await Fade(audioSource, 0f, audioSource.volume, sound.FadeInTime, eitherCts.Token);
					} catch (OperationCanceledException) { }
				}
				instantCt.ThrowIfCancellationRequested();

				// Play until sound stops or cancellation is requested
				try {
					await WaitUntilAudioStops(audioSource, eitherCts.Token);
				} catch (OperationCanceledException) { }
				instantCt.ThrowIfCancellationRequested();

				// Fade out
				if (audioSource.isPlaying && sound.Loop && sound.FadeOutTime > 0f) {
					await Fade(audioSource, audioSource.volume, 0f, sound.FadeOutTime, instantCt);
				}
			} finally {
				if (audioSource != null) {
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(audioSource);
					else
						UnityEngine.Object.DestroyImmediate(audioSource);
				}
			}
		}

		public static async Task WaitUntilAudioStops(AudioSource audioSource, CancellationToken ct)
		{
			while (audioSource != null && (audioSource.isPlaying || (Application.isPlaying && !Application.isFocused))) {
				await Task.Yield();
				ct.ThrowIfCancellationRequested();
			}
		}
	}
}
