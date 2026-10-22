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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CollisionSoundComponent)), CanEditMultipleObjects]
	public class CollisionSoundInspector : MechanicalSoundInspector
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			DrawSoundFields("MultiPlayMode");
			serializedObject.ApplyModifiedProperties();
			foreach (var selected in targets) {
				var component = (CollisionSoundComponent)selected;
				if (component.SoundAsset && component.SoundAsset.Loop) {
					EditorGUILayout.HelpBox("Collision sounds require a non-looping profile. Looping profiles will not play.", MessageType.Error);
					break;
				}
			}
			var sound = (CollisionSoundComponent)target;
			if (sound.SoundAsset && sound.SoundAsset is not CollisionSoundAsset)
				EditorGUILayout.HelpBox("Assign a collision sound profile, including its impact rules and samples.", MessageType.Error);
			if (!sound.GetComponent<ItemComponent>())
				EditorGUILayout.HelpBox("Place this component on the collider's item, or on the table for ball collisions.", MessageType.Error);
		}
	}
}
