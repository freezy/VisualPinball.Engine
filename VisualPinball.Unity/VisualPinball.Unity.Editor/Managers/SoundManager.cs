// Visual Pinball Engine
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

namespace VisualPinball.Unity.Editor
{
	class SoundManager : ManagerWindow<SoundListData>
	{
		protected override string DataTypeName => "Sound";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		private bool _displaySoundPosition = true;

		[MenuItem("Visual Pinball/Sound Manager", false, 104)]
		public static void ShowWindow()
		{
			GetWindow<SoundManager>();
		}

		protected override void OnButtonBarGUI()
		{
			EditorGUI.BeginChangeCheck();
			_displaySoundPosition = GUILayout.Toggle(_displaySoundPosition, "Display Sound Position");
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
			base.OnEnable();
			SceneView.duringSceneGui += OnSceneGUI;
		}

		protected void OnDisable()
		{
			SceneView.duringSceneGui -= OnSceneGUI;
		}

		private bool _shouldDisplaySoundPosition => (_table != null && _displaySoundPosition && _selectedItem != null && _selectedItem.SoundData.OutputTarget == SoundOutTypes.Table);

		private void Update()
		{
			if (_shouldDisplaySoundPosition) {
				SceneView.RepaintAll();
			}
		}

		void OnSceneGUI(SceneView sceneView)
		{
			//Draw the sound position based on Balance/Fade data
			if (_shouldDisplaySoundPosition) {
				var bb = _table.Item.BoundingBox;
				var sndData = _selectedItem.SoundData;
				Vector3 center = new Vector3((bb.Right - bb.Left) * 0.5f, (bb.Bottom - bb.Top) * 0.5f, (bb.ZHigh - bb.ZLow) * 0.5f);
				center = _table.gameObject.transform.TransformPoint(center);
				Vector3 size = new Vector3(bb.Width, bb.Height, bb.Depth);
				size = _table.gameObject.transform.TransformVector(size);
				center.x += size.x * 0.5f * sndData.Balance.PercentageToRatio();
				center.z += size.z * 0.5f * sndData.Fade.PercentageToRatio();
				Handles.color = Color.grey;
				Handles.SphereHandleCap(-1, center, Quaternion.identity, HandleUtility.GetHandleSize(center) * 0.2f, EventType.Repaint);
				Handles.DrawWireDisc(center, Vector3.up, Mathf.Repeat(Time.realtimeSinceStartup * 0.5f, size.magnitude * 0.25f));
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
