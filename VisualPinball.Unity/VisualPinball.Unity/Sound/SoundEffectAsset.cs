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
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity
{
    [CreateAssetMenu(fileName = "Sound Effect", menuName = "Visual Pinball/Sound/Sound Effect Asset", order = 102)]
    public class SoundEffectAsset : SoundAsset
    {
        public bool Loop => _loop;

        [SerializeField] private Vector2 _volumeRange = new(1f, 1f);
        [SerializeField] private Vector2 _pitchRange = new(1f, 1f);
        [SerializeField] private bool _loop;
        [SerializeField, Range(0, 10f)] private float _fadeInTime;
        [SerializeField, Range(0, 10f)] private float _fadeOutTime;

        public override void ConfigureAudioSource(AudioSource audioSource)
        {
            base.ConfigureAudioSource(audioSource);
            audioSource.volume = UnityEngine.Random.Range(_volumeRange.x, _volumeRange.y);
            audioSource.pitch = UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y);
        }

        public async Task Play(GameObject audioObj, CancellationToken allowFadeOutCt, CancellationToken instantCt, float volume = 1f)
        {
            var audioSource = audioObj.AddComponent<AudioSource>();

            try
            {
                ConfigureAudioSource(audioSource);
                audioSource.volume *= volume;
                audioSource.Play();
                using var eitherCts = CancellationTokenSource.CreateLinkedTokenSource(allowFadeOutCt, instantCt);

                // Fade in
                if (_loop && _fadeInTime > 0f)
                {
                    try
                    {
                        await Fade(audioSource, 0f, audioSource.volume, _fadeInTime, eitherCts.Token);
                    }
                    catch (OperationCanceledException) { }
                }
                instantCt.ThrowIfCancellationRequested();

                // Play until sound stops or cancellation is requested
                try
                {
                    await WaitUntilAudioStops(audioSource, eitherCts.Token);
                }
                catch (OperationCanceledException) { }
                instantCt.ThrowIfCancellationRequested();

                // Fade out
                if (audioSource != null && audioSource.isPlaying && _loop && _fadeOutTime > 0f)
                {
                    await Fade(audioSource, audioSource.volume, 0f, _fadeOutTime, instantCt);
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
        public static async Task Fade(AudioSource audioSource, float fromVolume, float toVolume, float duration, CancellationToken ct)
        {
            float progress = 0f;
#if UNITY_EDITOR
            // Time.deltaTime doesn't really work in the editor outside play mode
            var lastTime = EditorApplication.timeSinceStartup;
#endif
            while (progress < 1f)
            {
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
        public async Task PlayInEditorPreviewScene(CancellationToken allowFadeOutCt, CancellationToken instantCt)
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

        public static async Task WaitUntilAudioStops(AudioSource audioSource, CancellationToken ct)
        {
            while (audioSource != null && (audioSource.isPlaying || (Application.isPlaying && !Application.isFocused)))
            {
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}