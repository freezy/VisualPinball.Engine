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

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

using System;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(WPCEmuGameEngineAuthoring))]
	public class WPCEmuGameEngineInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var gameEngineAuthoring = (WPCEmuGameEngineAuthoring)target;
			var gameEngine = (WPCEmuGamelogicEngine)gameEngineAuthoring.GameEngine;

			EditorGUI.BeginChangeCheck();
			var selectedIndex = EditorGUILayout.Popup("Game", Array.IndexOf(gameEngine.SupportedGames, gameEngineAuthoring.Name), gameEngine.SupportedGames);
			if (EditorGUI.EndChangeCheck())
			{
				gameEngineAuthoring.Name = gameEngine.SupportedGames[selectedIndex];

				if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets"))
				{
					AssetDatabase.CreateFolder("Assets", "StreamingAssets");
					AssetDatabase.Refresh();
				}
			}

			if (gameEngineAuthoring.Name != null)
			{
				EditorGUILayout.LabelField($"Add ROM \"{gameEngine.RomFilename}\" to StreamingAssets");
			}
			else
			{
				EditorGUILayout.LabelField("No ROM selected");
			}
		}
	}
}
