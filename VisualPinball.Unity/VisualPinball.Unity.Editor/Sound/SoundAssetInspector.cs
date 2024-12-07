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

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SoundAsset)), CanEditMultipleObjects]
	public class SoundAssetInspector : UnityEditor.Editor
	{
		private SerializedProperty _nameProperty;
		private SerializedProperty _descriptionProperty;
		private SerializedProperty _volumeCorrectionProperty;
		private SerializedProperty _clipsProperty;
		private SerializedProperty _clipSelectionProperty;
		private SerializedProperty _randomizePitchProperty;
		private SerializedProperty _randomizeVolumeProperty;
		private SerializedProperty _loopProperty;
		private SerializedProperty _fadeInDurationProperty;
		private SerializedProperty _fadeOutDurationProperty;

		private SoundAsset _soundAsset;	
		
		private const float ButtonHeight = 30;
		private const float ButtonWidth = 50;

		private CancellationTokenSource allowFadeCts;
		private CancellationTokenSource instantCts;
		private Task playTask;
		private bool isPlaying;

		private void OnEnable()
		{
			_nameProperty = serializedObject.FindProperty(nameof(SoundAsset.Name));
			_descriptionProperty = serializedObject.FindProperty(nameof(SoundAsset.Description));
			_volumeCorrectionProperty = serializedObject.FindProperty(nameof(SoundAsset.VolumeCorrection));
			_clipsProperty = serializedObject.FindProperty(nameof(SoundAsset.Clips));
			_clipSelectionProperty = serializedObject.FindProperty(nameof(SoundAsset.ClipSelection));
			_randomizePitchProperty = serializedObject.FindProperty(nameof(SoundAsset.RandomizePitch));
			_randomizeVolumeProperty = serializedObject.FindProperty(nameof(SoundAsset.RandomizeVolume));
			_loopProperty = serializedObject.FindProperty(nameof(SoundAsset.Loop));
			_fadeInDurationProperty = serializedObject.FindProperty(nameof(SoundAsset.FadeInTime));
			_fadeOutDurationProperty = serializedObject.FindProperty(nameof(SoundAsset.FadeOutTime));

			//_editorAudioMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Resources/EditorMixer.mixer");
			//_editorAudioSource.outputAudioMixerGroup = _editorAudioMixer.outputAudioMixerGroup;
			
			_soundAsset = target as SoundAsset;
		}

		private async void OnDisable()
		{
			if (isPlaying)
				await Stop(allowFadeOut: false);
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(_nameProperty, true);

			using (var horizontalScope = new GUILayout.HorizontalScope())
			{
				EditorGUILayout.PropertyField(_descriptionProperty, GUILayout.Height(100));
			}
			
			EditorGUILayout.PropertyField(_volumeCorrectionProperty, true);
			EditorGUILayout.PropertyField(_clipsProperty);
			EditorGUILayout.PropertyField(_clipSelectionProperty, true);
			EditorGUILayout.PropertyField(_randomizePitchProperty, true);
			EditorGUILayout.PropertyField(_randomizeVolumeProperty, true);
			EditorGUILayout.PropertyField(_loopProperty);
			EditorGUILayout.PropertyField(_fadeInDurationProperty);
			EditorGUILayout.PropertyField(_fadeOutDurationProperty);

			serializedObject.ApplyModifiedProperties();

			// center button
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (PlayStopButton()) {
				if (isPlaying)
					_ = Stop(allowFadeOut: true);
				else
					playTask = Play();

			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private async Task Play()
		{
			try {
				isPlaying = true;
				allowFadeCts = new();
				instantCts = new();
				await SoundUtils.PlayInEditorPreviewScene(_soundAsset, allowFadeCts.Token, instantCts.Token);
			} catch (OperationCanceledException) { }
			finally {
				allowFadeCts.Dispose();
				allowFadeCts = null;
				instantCts.Dispose();
				instantCts = null;
				isPlaying = false;
			}
		}

		private async Task Stop(bool allowFadeOut)
		{
			if (isPlaying) {
				if (allowFadeOut)
					allowFadeCts.Cancel();
				else
					instantCts.Cancel();
				await playTask;
			}
		}
		
		private bool PlayStopButton()
		{
			return isPlaying
				? GUILayout.Button(new GUIContent("Stop", Icons.StopButton(IconSize.Small, IconColor.Orange)),
					GUILayout.Height(ButtonHeight), GUILayout.Width(ButtonWidth))
				: GUILayout.Button(new GUIContent("Play", Icons.PlayButton(IconSize.Small, IconColor.Orange)),
					GUILayout.Height(ButtonHeight), GUILayout.Width(ButtonWidth));
		}
	}
}

