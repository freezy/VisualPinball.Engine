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
using UnityEditor;
using UnityEngine.Audio;

namespace VisualPinball.Unity.Editor
{

	
	[CustomEditor(typeof(SoundAsset)), CanEditMultipleObjects]
	public class SoundsInspector : UnityEditor.Editor
	{
		SerializedProperty Name;
		SerializedProperty Description;
		SerializedProperty VolumeCorrection;
		SerializedProperty Clips;
		SerializedProperty ClipSelection;
		SerializedProperty RandomizePitch;
		SerializedProperty RandomizeSpeed;
		SerializedProperty RandomizeVolume;

		private const string _playButtonText = "Play";
		private const string _loopButtonText = "Loop";
		private IconColor _loopButtonColor = IconColor.Gray;
		private const string _stopButtonText = "Stop";
		private const float  _buttonHeight = 30;
		private const float  _buttonWidth = 50;
		private const float _clipDelayModifier = 0.2f;//helps set the time between playing audio clips, if the current clip is less than 1 second in length
		private int _clipIndex;
		private bool _loop = false;
		private AudioClip[] _clipArray;
		private GameObject[] _soundObjects;
		private int _soundObjectsLength = 0;
		private float _nextStartTime = (float)AudioSettings.dspTime + _clipDelayModifier;
		private int _nextClip = 0;
		private bool _clipPlaying = false;


		private void OnEnable()
		{
			Name = serializedObject.FindProperty(nameof(SoundAsset.Name));
			Description = serializedObject.FindProperty(nameof(SoundAsset.Description));
			VolumeCorrection = serializedObject.FindProperty(nameof(SoundAsset.VolumeCorrection));
			Clips = serializedObject.FindProperty(nameof(SoundAsset.Clips));
			ClipSelection = serializedObject.FindProperty(nameof(SoundAsset.ClipSelection));
			RandomizePitch = serializedObject.FindProperty(nameof(SoundAsset.RandomizePitch));
			RandomizeSpeed = serializedObject.FindProperty(nameof(SoundAsset.RandomizeSpeed));
			RandomizeVolume = serializedObject.FindProperty(nameof(SoundAsset.RandomizeVolume));

		}

		private void OnDisable()
		{
			EditorApplication.update -= Update;
		}

		private void OnDestroy()
		{
		}

		public override void OnInspectorGUI()
		{

			EditorApplication.update += Update;
			serializedObject.Update();

			EditorGUILayout.PropertyField(Name, true);

			using (var horizontalScope = new GUILayout.HorizontalScope())
			{
				
				EditorGUILayout.PropertyField(Description, GUILayout.Height(100));
			}
			
			EditorGUILayout.PropertyField(VolumeCorrection, true);
			EditorGUILayout.PropertyField(Clips, true);
			EditorGUILayout.PropertyField(ClipSelection, true);
			EditorGUILayout.PropertyField(RandomizePitch, true);
			EditorGUILayout.PropertyField(RandomizeSpeed, true);
			EditorGUILayout.PropertyField(RandomizeVolume, true);

			EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


			GUILayout.BeginHorizontal();
			GUILayout.Space(100);

			_clipArray = new AudioClip[Clips.arraySize];
			for (int i = 0; i < Clips.arraySize; i++)
			{
				_clipArray[i] = (AudioClip)Clips.GetArrayElementAtIndex(i).objectReferenceValue;
			}


			if (GUILayout.Button(new GUIContent(_playButtonText, Icons.PlayButton(IconSize.Small, IconColor.Orange)), GUILayout.Height(_buttonHeight), GUILayout.Width(_buttonWidth)))
			{
				if (_loop == false && _clipPlaying == false) //play single clip only when loop of clips not playing and current single clip is not already playing
				{
					if (ClipSelection.intValue == 0)
					{ PlayRoundRobin(_clipArray); }
					else { PlayRandom(_clipArray); }


				}
			}

			if (GUILayout.Button(new GUIContent(_loopButtonText, Icons.PlayButton(IconSize.Small, _loopButtonColor)), GUILayout.Height(_buttonHeight), GUILayout.Width(_buttonWidth)))
			{
				//if loop then sound objects are setup in the Update method
				if (_loop)
				{
					_loopButtonColor = IconColor.Gray;
					Destroy();//destroy temporary game objects then set loop toggle to false
				}
				else
				{
					_loop = true;
					_loopButtonColor = IconColor.Orange;
				}
			}

			if (GUILayout.Button(new GUIContent(_stopButtonText, Icons.StopButton(IconSize.Small, IconColor.Orange)), GUILayout.Height(_buttonHeight), GUILayout.Width(_buttonWidth)))
			{
				Stop();
				_loopButtonColor = IconColor.Gray;
			}


			GUILayout.EndHorizontal();

			serializedObject.ApplyModifiedProperties();

		}


		private void Stop()
		{
			if (_loop)
			{
				for (int i = 0; i < _clipArray.Length; i++)
				{
					if (GameObject.Find($"Sound{i}") != null)
					{
						AudioSource source = GameObject.Find($"Sound{i}").GetComponent<AudioSource>();
						source.Stop();
						Destroy();//destroy temporary game objects then set loop toggle to false
					}
				}
			}
			else
			{
				GameObject ob = GameObject.Find("Sound");

				if (ob != null)
				{

					AudioSource source = ob.GetComponent<AudioSource>();
					source.Stop();
					Destroy();
				}

			}
		}

		private void Destroy()
		{
			if (_loop)
			{
				for (int i = 0; i < _clipArray.Length; i++)
				{
					GameObject ob = GameObject.Find($"Sound{i}");
					DestroyImmediate(ob);

				}

				_soundObjectsLength = 0;
				_nextStartTime = (float)AudioSettings.dspTime + _clipDelayModifier;
				_loop = false;

			}
			else
			{
				_clipPlaying = false;
				GameObject ob = GameObject.Find("Sound");
				DestroyImmediate(ob);
			}
			


		}
		private void PlayRoundRobin(AudioClip[] _clipArray)
		{
			if (_clipIndex < _clipArray.Length)
			{
				PlayClip(_clipArray[_clipIndex]);
				_clipIndex++;
			}

			else
			{
				_clipIndex = 0;
				PlayClip(_clipArray[_clipIndex]);
				_clipIndex++;
			}
		}



		private void PlayRandom(AudioClip[] _clipArray)
		{
			if (_clipIndex == _clipArray.Length)
			{
				_clipIndex = 0;
			}

			if (_clipArray.Length > 1)//randomize clip to play only if more than one clip
			{
				_clipIndex = Random.Range(0, _clipArray.Length);
				PlayClip(_clipArray[_clipIndex]);
			}
			else 
			{ PlayClip(_clipArray[_clipIndex]); }

		}

		
		private void PlayClip(AudioClip clip)
		{
			GameObject tempGameObject = new GameObject("Sound");
			tempGameObject = GetTempGameObject(tempGameObject);
			AudioSource audioSource = tempGameObject.GetComponent<AudioSource>();
			audioSource.clip = clip;
			_clipPlaying = true;
			audioSource.Play();
			

		}
		private  GameObject[] GetSoundObjects()
		{
			GameObject[] objects = new GameObject[_clipArray.Length];

			for (int i = 0; i < _clipArray.Length; i++)
			{
				GameObject tempGameObject = new GameObject($"Sound{i}");
				tempGameObject = GetTempGameObject(tempGameObject);
				AudioSource audioSource = tempGameObject.GetComponent<AudioSource>();
				audioSource.clip = _clipArray[i];
				objects[i] = tempGameObject;
			}

			_soundObjectsLength = objects.Length;

			return objects;
			

		}

		// Create a temporary gameobject so that a temporary audiosource can be attached to it, in order to play audioclips
		private GameObject GetTempGameObject(GameObject tempGameObject)
		{

			Vector3 position = new Vector3(5, 1, 2);
			float volume = VolumeCorrection.floatValue;
			float pitchModifier = RandomizePitch.floatValue;
			float speedModifier = RandomizeSpeed.floatValue;
			float volumeModifier = RandomizeVolume.floatValue;
			
			if (volumeModifier != 1)
			{ volume = volumeModifier; }

			tempGameObject.transform.position = position;
			//_tempGameObject.hideFlags = HideFlags.HideAndDontSave; //dont save object in editor and dont show in heirarchy
			AudioSource tempAudioSource = (AudioSource)tempGameObject.AddComponent(typeof(AudioSource));
			tempAudioSource.spatialBlend = 1f;
			tempAudioSource.pitch =  pitchModifier;
			tempAudioSource.volume = volume;
			float value;

			AudioMixer audioMixer = Resources.Load<AudioMixer>("SoundMixer");
			AudioMixerGroup[] audioMixGroup = audioMixer.FindMatchingGroups("Master");
			audioMixGroup[0].audioMixer.SetFloat("pitchShifter", speedModifier);
			//audioMixGroup[0].audioMixer.GetFloat("pitchShifter", out value);
			//Debug.Log(value.ToString());
			tempAudioSource.outputAudioMixerGroup = audioMixGroup[0];

			return tempGameObject;

		}
		
		private void Update()
		{
			
			if (_loop)
			{
				//set up new array of gameobjects based on most current number of audioclips, if new loop is initiated
				if (_soundObjects == null || _soundObjectsLength == 0) 
				{ _soundObjects = GetSoundObjects();}

					if (AudioSettings.dspTime > _nextStartTime - 1)
					{
					    int index = ClipSelection.intValue == 0 ? _nextClip : Random.Range(0, _soundObjects.Length);
						AudioSource source = _soundObjects[index].GetComponent<AudioSource>();
						AudioClip clipToPlay = source.clip;
						source.PlayScheduled(_nextStartTime);

						// Checks how long the Clip will last and updates the Next Start Time with a new value
						float duration = clipToPlay.samples / clipToPlay.frequency;
						float clipLength = clipToPlay.length;

						if (clipLength < 1)
						{ clipLength = clipLength + _clipDelayModifier; }

						_nextStartTime = _nextStartTime + clipLength;

						// Increase the clip index number, reset if it runs out of clips
						_nextClip = _nextClip < _soundObjects.Length - 1 ? _nextClip + 1 : 0;
						}
				
			   }
			
			else
			{
				GameObject ob = GameObject.Find("Sound");
				if (ob != null)
				{

					AudioSource source = ob.GetComponent<AudioSource>();
					if (source.isPlaying == false)
					{
						Destroy();

					}


				}

			}
		}

	}
}

