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
		/// Gets or creates the editor GameObject for playing sounds in the editor.
		///
		/// The hierarchy looks like that:
		///
		///  [scene root]
		///    |
		///    -- EditorScene
		///      |
		///       -- EditorAudio (with AudioSource component)
		/// </summary>
		/// <returns>AudioSource of the editor GameObject for test playing audio.</returns>
		private static AudioSource GetOrCreateAudioSource()
		{
			// todo check whether we'll instantiate those live in the future or rely on a provided prefab
			var editorSceneGo = SceneManager.GetActiveScene().GetRootGameObjects()
				.FirstOrDefault(go => go.name == "EditorScene");

			if (editorSceneGo == null) {
				editorSceneGo = new GameObject("EditorScene");
			}

			GameObject editorAudioGo = null;
			for (var i = 0; i < editorSceneGo.transform.childCount; i++) {
				var go = editorSceneGo.transform.GetChild(i).gameObject;
				if (go.name != "EditorAudio") {
					continue;
				}
				editorAudioGo = go;
				break;
			}

			if (editorAudioGo == null) {
				editorAudioGo = new GameObject("EditorAudio");
				editorAudioGo.transform.SetParent(editorSceneGo.transform);
			}

			var audioSource = editorAudioGo.GetComponent<AudioSource>();
			if (!audioSource) {
				audioSource = editorAudioGo.AddComponent<AudioSource>();
			}

			return audioSource;
		}
	}
}

