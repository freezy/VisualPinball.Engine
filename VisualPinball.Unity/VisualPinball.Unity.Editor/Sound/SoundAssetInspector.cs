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

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
		
		private SoundAsset _soundAsset;
		
		private AudioSource _editorAudioSource;
		//private AudioMixer _editorAudioMixer;
		
		private const float ButtonHeight = 30;
		private const float ButtonWidth = 50;
		
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

			_editorAudioSource = GetOrCreateAudioSource();
			//_editorAudioMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Resources/EditorMixer.mixer");
			//_editorAudioSource.outputAudioMixerGroup = _editorAudioMixer.outputAudioMixerGroup;
			
			_soundAsset = target as SoundAsset;
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
			
			serializedObject.ApplyModifiedProperties();

			// center button
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (PlayStopButton()) {
				PlayStop();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		private void PlayStop()
		{
			if (_editorAudioSource.isPlaying) {
				_soundAsset.Stop(_editorAudioSource);
			} else {
				_soundAsset.Play(_editorAudioSource);
			}
		}
		
		private bool PlayStopButton()
		{
			return _editorAudioSource.isPlaying
				? GUILayout.Button(new GUIContent("Stop", Icons.StopButton(IconSize.Small, IconColor.Orange)),
					GUILayout.Height(ButtonHeight), GUILayout.Width(ButtonWidth))
				: GUILayout.Button(new GUIContent("Play", Icons.PlayButton(IconSize.Small, IconColor.Orange)),
					GUILayout.Height(ButtonHeight), GUILayout.Width(ButtonWidth));
		}

		/// <summary>
		/// Gets or creates the AudioSource for playing sounds in the editor.
		/// The object containing the AudioSource is created in a new, additively loaded scene
		/// to avoid making changes to the user's currently open scene.
		/// </summary>
		/// <returns>AudioSource for previewing audio assets in the editor</returns>
		private static AudioSource GetOrCreateAudioSource()
		{
			Scene editorScene = GetOrCreatePreviewScene();
			GameObject editorAudio = GetOrCreatePreviewAudioObject(editorScene);
			if (!editorAudio.TryGetComponent<AudioSource>(out var audioSource)) {
				audioSource = editorAudio.AddComponent<AudioSource>();
			}
			return audioSource;
		}

		private static Scene GetOrCreatePreviewScene()
		{
			const string sceneName = "VpeEditorScene";

			for (int i = 0; i < SceneManager.loadedSceneCount; i++) {
				Scene scene = SceneManager.GetSceneAt(i);
				if (scene.name == sceneName)
					return scene;
			}

			Scene previewScene = EditorSceneManager.NewPreviewScene();
			previewScene.name = sceneName;
			return previewScene;
		}

		private static GameObject GetOrCreatePreviewAudioObject(Scene previewScene)
		{
			const string audioObjName = "AudioPreview";

			var audioObj = previewScene.GetRootGameObjects()
				.FirstOrDefault(go => go.name == audioObjName);

			if (audioObj == null) {
				audioObj = new GameObject(audioObjName);
				SceneManager.MoveGameObjectToScene(audioObj, previewScene);
			}

			return audioObj;
		}
	}
}

