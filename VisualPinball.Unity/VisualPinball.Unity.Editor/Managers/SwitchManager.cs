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
	class SwitchManager : ManagerWindow<SwitchListData>
	{
		protected override string DataTypeName => "Switch";

		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Switch Manager", false, 105)]
		public static void ShowWindow()
		{
			GetWindow<SwitchManager>();
		}

		protected override void OnButtonBarGUI()
		{
		}

		protected override void OnDataDetailGUI()
		{
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Switch Manager", EditorGUIUtility.IconContent("SceneViewAudio").image);
			base.OnEnable();
			SceneView.duringSceneGui += OnSceneGUI;
		}

		protected void OnDisable()
		{
			SceneView.duringSceneGui -= OnSceneGUI;
		}

		//private bool _shouldDisplaySoundPosition => (_table != null && _displaySoundPosition && _selectedItem != null && _selectedItem.SoundData.OutputTarget == SoundOutTypes.Table);

		private void Update()
		{
			//if (_shouldDisplaySoundPosition) {
			//	SceneView.RepaintAll();
			//}
		}

		void OnSceneGUI(SceneView sceneView)
		{ 
		}
	}
}
