// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RollingSoundMaterialAsset))]
	public sealed class RollingSoundMaterialInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			var material = (RollingSoundMaterialAsset)target;
			var tags = new HashSet<BallSoundMaterialAsset>();
			foreach (var row in material.Responses) {
				if (row == null) continue;
				if (!row.BallMaterial) EditorGUILayout.HelpBox("Assign a ball material to each response row.", MessageType.Warning);
				else if (!tags.Add(row.BallMaterial)) EditorGUILayout.HelpBox("Duplicate ball-material row: " + row.BallMaterial.name, MessageType.Error);
				if (!row.Loop || !row.Loop.Loop) EditorGUILayout.HelpBox("Rolling requires a looping sound bank. A missing or non-looping bank is silent.", MessageType.Warning);
				if ((row.Enter && row.Enter.Loop) || (row.Exit && row.Exit.Loop)) EditorGUILayout.HelpBox("Enter and Exit banks must not loop.", MessageType.Error);
				if (row.MinimumSpeed < 0 || row.AttackSeconds < 0 || row.ReleaseSeconds < 0 || row.CrossfadeSeconds < 0) EditorGUILayout.HelpBox("Speed thresholds and fade times must be nonnegative.", MessageType.Error);
			}
			EditorGUILayout.HelpBox("Unmapped ball materials are silent unless an explicit fallback is selected. Surface tags do not change physical coefficients.", MessageType.Info);
		}
	}
}
