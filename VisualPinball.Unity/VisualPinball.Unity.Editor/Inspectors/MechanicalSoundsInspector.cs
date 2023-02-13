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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MechSounds;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(MechSoundsComponent)), CanEditMultipleObjects]

	//public class MechanicalSoundInspector : UnityEditor.Editor, ISoundEmitter
	public class MechanicalSoundInspector : MainInspector<MechSoundsData, MechSoundsComponent>
	{

		private MechSoundsComponent _myTarget;
		private SerializedProperty _soundList;
		private const float _buttonWidth = 150;


		protected override void OnEnable()
		{

			_myTarget = (MechSoundsComponent)target;
			_soundList = serializedObject.FindProperty(nameof(MechSoundsComponent.SoundList));
			InitTriggers();
			SetSoundTriggers();
			InitVolumeEmitters();
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors())
			{
				return;
			}

			serializedObject.Update();

			EditorGUILayout.LabelField("Current Sounds");
			
			GUILayout.BeginVertical("Box");
			for (int i = 0; i < _soundList.arraySize; i++)
			{
				SerializedProperty _element = _soundList.GetArrayElementAtIndex(i);
				SerializedProperty _triggerProperty = _element.FindPropertyRelative("Trigger");
				SerializedProperty _soundProperty = _element.FindPropertyRelative("Sound");
				SerializedProperty _volumeSelectionProperty = _element.FindPropertyRelative("Volume");
				SerializedProperty _volumeProperty = _element.FindPropertyRelative("VolumeValue");
				SerializedProperty _actionSelectionProperty = _element.FindPropertyRelative("Action");
				SerializedProperty _fadeProperty = _element.FindPropertyRelative("Fade");

				_triggerProperty.intValue = EditorGUILayout.Popup("Trigger", _triggerProperty.intValue, GetTriggerOptions(_myTarget.AvailableTriggers));
				_myTarget.SelectedTrigger = GetSelectedTrigger(_triggerProperty.intValue);

				EditorGUILayout.Space(5);
				_soundProperty.objectReferenceValue = EditorGUILayout.ObjectField("Sound", _soundProperty.objectReferenceValue, typeof(SoundAsset), true);
				
				EditorGUILayout.Space(5);
				_volumeSelectionProperty.intValue = EditorGUILayout.Popup(new GUIContent("Volume", "Depends on trigger selected: \n 'Fixed'-Not dependent on any playfield action. \n 'Ball Velocity'- Gameplay-related (collision)."), _volumeSelectionProperty.intValue, GetEmitterOptions(_myTarget.AvailableEmitters));

				EditorGUILayout.Space(5);
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("", GUILayout.Width(EditorGUIUtility.labelWidth));
				_volumeProperty.floatValue = EditorGUILayout.Slider("", _volumeProperty.floatValue, 0.1f, 2);
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(5);
				EditorGUILayout.PropertyField(_actionSelectionProperty);

				EditorGUILayout.Space(5);
				EditorGUILayout.BeginHorizontal();
				_fadeProperty.floatValue = EditorGUILayout.Slider("Fade", _fadeProperty.floatValue, 0, 300);
				GUILayout.Label("ms", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(15);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Remove this sound", GUILayout.Width(_buttonWidth)))
				{
					_soundList.DeleteArrayElementAtIndex(i);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

			}//end soundlist loop
			GUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Add a new sound", GUILayout.Width(_buttonWidth)))
			{
				_myTarget.SoundList.Add(new MechSoundsData.MechSound());
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			
			serializedObject.ApplyModifiedProperties();
		}


		#region Initialize/Set methods
		private void InitTriggers()
		{
			if (_myTarget.AvailableTriggers == null || _myTarget.AvailableTriggers.Length == 0)
			{
				//Debug.Log("Initializing the SoundTriggers array...");
				_myTarget.AvailableTriggers = new SoundTrigger[1];
			}
		}
		private void SetSoundTriggers()
		{
			//flipper on button press event
			string CoilOnId = "coil_on";
			string CoilOnName = "Coil On";
			//flipper release button press event
			string CoilOffId = "coil_off";
			string CoilOffName = "Coil Off";
			//ball collision with the flipper
			string BallCollisionId = "ball_collision";
			string BallCollisionName = "Ball Collision";

			string[] Ids = new string[] { CoilOnId, CoilOffId, BallCollisionId };
			string[] Names = new string[] { CoilOnName, CoilOffName, BallCollisionName };

			int index = Ids.Length;
			SoundTrigger soundTrigger;
			SoundTrigger[] triggers = new SoundTrigger[index];

			for (int i = 0; i < index; i++)
			{
				soundTrigger = new SoundTrigger();
				soundTrigger.Id = Ids[i];
				soundTrigger.Name = Names[i];	
				triggers[i] = soundTrigger;
			}

			_myTarget.AvailableTriggers = triggers;
			_myTarget.SelectedTrigger = triggers[0];
			serializedObject.ApplyModifiedProperties();

		}

		private string[] GetTriggerOptions(SoundTrigger[] triggers)
		{
			int index = triggers.Length;
			string[] options = new string[index];

			for (int i = 0; i < index; i++)
			{
				options[i] = triggers[i].Name;
			}

			return options;
		}

		
		private SoundTrigger GetSelectedTrigger(int index)
		{
			SoundTrigger sTrigger = new SoundTrigger();
			sTrigger.Id = _myTarget.AvailableTriggers[index].Id;
			sTrigger.Name = _myTarget.AvailableTriggers[index].Name;
			
			return sTrigger;
		}

		private void InitVolumeEmitters()
		{
			if (_myTarget.AvailableEmitters == null || _myTarget.AvailableEmitters.Length == 0)
			{
				_myTarget.AvailableEmitters = new VolumeEmitter[1];
			}
		}

		private string[] GetEmitterOptions(VolumeEmitter[] volEmitters)
		{
			int index = volEmitters.Length;
			string[] options = new string[index];

			for (int i = 0; i < index; i++)
			{
				options[i] = volEmitters[i].Name;
			}

			return options;
		}
		#endregion
		
	}
}

