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
using System.Threading.Tasks;

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
		private const float _clipDelayModifier = 1f;//helps set the time between playing audio clips, if the current clip is less than 1 second in length
		private int _clipIndex;
		private bool _loop = false;
		private AudioClip[] _clipArray;
		private float _nextStartTime = (float)AudioSettings.dspTime + _clipDelayModifier;
		private int _nextClip = 0;
		private bool _clipPlaying = false;
		private GameObject _flipper;
		private AudioSource _audioSource;


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

			//get a gameobject with a mechsounds component attached, to play sounds from
			_flipper = GameObject.Find("Left Flipper");
			_audioSource = _flipper.GetComponent<AudioSource>();

			EditorApplication.update += Update;

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

			serializedObject.Update();

			EditorGUILayout.PropertyField(Name, true);

			using (var horizontalScope = new GUILayout.HorizontalScope())
			{
				
				EditorGUILayout.PropertyField(Description, GUILayout.Height(100));
			}
			
			EditorGUILayout.PropertyField(VolumeCorrection, true);
		    EditorGUILayout.PropertyField(Clips);
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
				
				if (_loop)
				{
					_loopButtonColor = IconColor.Gray;
					Stop();
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

			//SetAudioProperties();
			DelayedUpdateAudio();

			serializedObject.ApplyModifiedProperties();

		}

		//attempt to update audiomixer pitch shifter value
		async void DelayedUpdateAudio()
		{
			await Task.Delay(1000);
			SetAudioProperties();
		}
		private void SetAudioProperties()
		{

			float volume = VolumeCorrection.floatValue;
			float pitchModifier = RandomizePitch.floatValue;
			float speedModifier = RandomizeSpeed.floatValue;
			float volumeModifier = RandomizeVolume.floatValue;

			if (volumeModifier != 1)
			{ volume = volumeModifier; }

			_audioSource.spatialBlend = 1f;
			_audioSource.pitch = pitchModifier;
			_audioSource.volume = volume;

			float value;

			//AudioMixer audioMixer = Resources.Load<AudioMixer>("SoundMixer");
			AudioMixerGroup audioMixGroup = _audioSource.outputAudioMixerGroup;//audioMixer.FindMatchingGroups("Master");
			audioMixGroup.audioMixer.SetFloat("pitchShifter", speedModifier);//speedModifier);//speedModifier);
			audioMixGroup.audioMixer.GetFloat("pitchShifter", out value);
			Debug.Log(value.ToString());
			//_audioSource.outputAudioMixerGroup = audioMixGroup[0];

		}
		private void Stop()
		{

			if (_flipper != null)
			{
				_audioSource.Stop();

				if (_loop)
				{
					_nextStartTime = (float)AudioSettings.dspTime + _clipDelayModifier;
					_loop = false;
				}
				else
				{
					_clipPlaying = false;
				}
			}
		}
		private void PlayRoundRobin(AudioClip[] _clipArray)
		{
			var nextIndex = _clipIndex + 1 == _clipArray.Length ? 0 : _clipIndex + 1;
			PlayClip(_clipArray[nextIndex]);
			_clipIndex = nextIndex;

		}

		private void PlayRandom(AudioClip[] _clipArray)
		{
			PlayClip(_clipArray[Random.Range(0, _clipArray.Length)]);
		}

		private void PlayClip(AudioClip clip)
		{
			_audioSource.clip = clip;
			_clipPlaying = true;
			_audioSource.Play();
		}		
		private void Update()
		{
			
			if (_loop)
			{
					if (AudioSettings.dspTime > _nextStartTime - 1)
					{
					    int index = ClipSelection.intValue == 0 ? _nextClip : Random.Range(0, _clipArray.Length);
					    _audioSource.clip = _clipArray[index];
					     AudioClip clipToPlay = _audioSource.clip;
					    _audioSource.PlayScheduled(_nextStartTime);

						// Checks how long the Clip will last and updates the Next Start Time with a new value
						float duration = clipToPlay.samples / clipToPlay.frequency;
						float clipLength = clipToPlay.length;

						if (clipLength < 1)
						{ clipLength = clipLength + _clipDelayModifier; }

						_nextStartTime = _nextStartTime + clipLength;

						// Increase the clip index number, reset if it runs out of clips
						_nextClip = _nextClip < _clipArray.Length - 1 ? _nextClip + 1 : 0;
						}
				
			   }
			
			else
			{
				if (_flipper != null)
				{
					if (_audioSource.isPlaying == false)
					{
						Stop();

					}
				}

			}
		}

	}
}

