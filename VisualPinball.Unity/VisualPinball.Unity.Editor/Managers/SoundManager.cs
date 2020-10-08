﻿// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using NLog;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Editor.Utils;

namespace VisualPinball.Unity.Editor
{
	class SoundManager : ManagerWindow<SoundListData>
	{
		protected override string DataTypeName => "Sound";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Sound positions display
		/// </summary>
		private bool _displaySoundPosition = true;
		private bool _displayAllSounds = false;

		/// <summary>
		/// Auto framing, going to Top view and frame on whole table when focused to ease sound position visualization
		/// </summary>
		private bool _autoFrame = true;
		private bool _needFraming = false;

		/// <summary>
		/// Table & selected sound position & size used for display
		/// </summary>
		private Vector3 _tableCenter = Vector3.zero;
		private Vector3 _tableSize = Vector3.zero;
		private Vector3 _selectedSoundPos = Vector3.zero;
		private float  _selectedSoundSize = 0.0f;

		private readonly Color _selectedColor = Color.yellow;
		private readonly Color _unselectedColor = new Color(0.25f, 0.25f, 0.25f, 0.75f);
		private GUIContent _iconContent;
		private GUIStyle _iconStyle;

		[MenuItem("Visual Pinball/Sound Manager", false, 104)]
		public static void ShowWindow()
		{
			GetWindow<SoundManager>();
		}

		protected override void OnButtonBarGUI()
		{
			EditorGUI.BeginChangeCheck();
			_autoFrame = GUILayout.Toggle(_autoFrame, "Auto Framing", GUILayout.ExpandWidth(false));
			if (EditorGUI.EndChangeCheck() && _autoFrame) {
				_needFraming = true;
			}

			EditorGUI.BeginChangeCheck();
			_displaySoundPosition = GUILayout.Toggle(_displaySoundPosition, "Display Sound Position", GUILayout.ExpandWidth(false));
			if (_displaySoundPosition) {
				_displayAllSounds = GUILayout.Toggle(_displayAllSounds, "Display All Sounds", GUILayout.ExpandWidth(false));
			}
			if (EditorGUI.EndChangeCheck()) {
				SceneView.RepaintAll();
			}

		}

		public static string[] _soundOutTypeStrings = {
			"Table",
			"Backglass",
		};
		private static byte[] _soundOutTypeValues = {
			SoundOutTypes.Table,
			SoundOutTypes.Backglass,
		};

		protected override void OnDataDetailGUI()
		{
			DropDownField("Output Target", ref _selectedItem.SoundData.OutputTarget, _soundOutTypeStrings, _soundOutTypeValues);
			SliderField("Volume", ref _selectedItem.SoundData.Volume, -100, 100);
			SliderField("Balance", ref _selectedItem.SoundData.Balance, -100, 100);
			SliderField("Fade (Rear->Front)", ref _selectedItem.SoundData.Fade, -100, 100);
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Sound Manager", EditorGUIUtility.IconContent("SceneViewAudio").image);
			_iconContent = new GUIContent() {
				image = EditorGUIUtility.IconContent("AudioSource Gizmo").image
			};
			_iconStyle = new GUIStyle() {
				alignment = TextAnchor.MiddleCenter,
				imagePosition = ImagePosition.ImageOnly,
				fixedHeight = 25.0f,
				fixedWidth = 25.0f
			};
			base.OnEnable();
			SceneView.duringSceneGui += OnSceneGUI;
		}

		protected void OnDisable()
		{
			SceneView.duringSceneGui -= OnSceneGUI;
		}

		protected override void OnFocus()
		{
			base.OnFocus();

			if (_table == null || _table.gameObject == null) return;

			Selection.activeObject = _table.gameObject;

			if (_autoFrame) {
				_needFraming = true;
			}

			SceneView.RepaintAll();
		}

		private void OnLostFocus()
		{
			SceneView.RepaintAll();
		}


		protected override void OnDataSelected() 
		{
			base.OnDataSelected();
			SceneView.RepaintAll();
		}


		private bool _shouldDisplaySoundPosition => (	_table != null && 
														Event.current.type == EventType.Repaint &&
														(EditorWindow.focusedWindow == this || (EditorWindow.focusedWindow == SceneView.lastActiveSceneView && Selection.activeObject == _table.gameObject)) && 
														_displaySoundPosition && 
														_selectedItem != null && 
														_selectedItem.SoundData.OutputTarget == SoundOutTypes.Table);


		//Draw the sound position based on Balance/Fade data
		private void RenderSound(SoundData data, bool selected)
		{
			var sndPos = _tableCenter;
			sndPos.x += _tableSize.x * 0.5f * data.Balance.PercentageToRatio();
			sndPos.y -= _tableSize.y * 0.25f;
			sndPos.z += _tableSize.z * 0.5f * data.Fade.PercentageToRatio();
			Color col = selected ? _selectedColor : _unselectedColor;

			//Disc size based on sound volume
			var minSphereSize = 0.25f;
			var maxSphereSize = _tableSize.magnitude * 0.5f;
			
			//Volume goes from -100 to 100 -> ratio
			var sphereSizeRatio = (data.Volume + 100) * 0.005f;
			var sphereSize = (sphereSizeRatio * (maxSphereSize - minSphereSize));
			col.a = 0.05f;
			Handles.color = col;
			Handles.DrawSolidDisc(sndPos, Vector3.up, sphereSize);

			//Sound Gizmo
			col.a = selected ? 1.0f : 0.25f;
			GUI.color = col;
			Handles.Label(sndPos, _iconContent, _iconStyle);

			if (selected) {
				_selectedSoundPos = sndPos;
				_selectedSoundSize = sphereSize;
			}
		}

		private void OnSceneGUI(SceneView sceneView)
		{
			if (_table == null) return;

			var bb = _table.Item.BoundingBox;
			var sndData = _selectedItem.SoundData;
			_tableCenter = new Vector3((bb.Right - bb.Left) * 0.5f, (bb.Bottom - bb.Top) * 0.5f, (bb.ZHigh - bb.ZLow) * 0.5f);
			_tableCenter = _table.gameObject.transform.TransformPoint(_tableCenter);
			_tableSize = new Vector3(bb.Width, bb.Height, bb.Depth);
			_tableSize = _table.gameObject.transform.TransformVector(_tableSize);

			if (_shouldDisplaySoundPosition) {
				if (_displayAllSounds) {
					foreach (var snd in _table.Sounds) {
						if (snd.Data != sndData) {
							RenderSound(snd.Data, false);
						}
					}
				}

				RenderSound(sndData, true);
			}

			//Ask for framing after _tableCenter calculation
			if (_needFraming) {
				//Frame to Top View 
				SceneViewFramer.FrameObjects(Selection.objects);
				var view = SceneView.lastActiveSceneView;
				var quat = Quaternion.identity;
				quat.SetLookRotation(Vector3.down);
				view.LookAt(_tableCenter, quat, Mathf.Max(_tableSize.x, _tableSize.z) * 1.1f);

				_needFraming = false;
			}
		}

		protected override void OnDataChanged(string undoName, SoundListData data)
		{
			OnDataChanged(undoName, data.SoundData);
		}

		private void OnDataChanged(string undoName, SoundData data)
		{
			RecordUndo(undoName, data);
		}

		private void RecordUndo(string undoName, SoundData data)
		{
			if (_table == null) { return; }

			// Run over table's sound scriptable object wrappers to find the one being edited and add to the undo stack
			foreach (var tableTex in _table.Sounds.SerializedObjects) {
				if (tableTex.Data == data) {
					Undo.RecordObject(tableTex, undoName);
					break;
				}
			}
		}

		protected override void AddNewData(string undoName, string newName)
		{
			Undo.RecordObject(_table, undoName);

			var newSnd = new Sound(newName);
			_table.Sounds.Add(newSnd);
			_table.Item.Data.NumSounds = _table.Sounds.Count;
		}

		protected override void RemoveData(string undoName, SoundListData data)
		{
			Undo.RecordObject(_table, undoName);

			_table.Sounds.Remove(data.Name);
			_table.Item.Data.NumSounds = _table.Sounds.Count;
		}

		protected override void RenameExistingItem(SoundListData data, string newName)
		{
			string oldName = data.SoundData.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Sound";
			RecordUndo(undoName, data.SoundData);

			data.SoundData.Name = newName;
		}

		protected override List<SoundListData> CollectData()
		{
			List<SoundListData> data = new List<SoundListData>();

			foreach (var snd in _table.Sounds) {
				var sndData = snd.Data;
				data.Add(new SoundListData { SoundData = sndData });
			}

			return data;
		}
	}
}
