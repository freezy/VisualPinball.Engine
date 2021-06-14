// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{

	[CustomEditor(typeof(DisplayAuthoring)), CanEditMultipleObjects]
	public class DisplayInspector : UnityEditor.Editor
	{
		public const float GameObjectScale = 0.5f;

		[NonSerialized] private DisplayAuthoring _mb;
		[NonSerialized] private DisplayAuthoring[] _mbs;

		protected void OnEnable()
		{
			_mb = target as DisplayAuthoring;
			_mbs = targets.Select(t => t as DisplayAuthoring).ToArray();
		}

		public override void OnInspectorGUI()
		{
			var color = EditorGUILayout.ColorField("Lit Color", _mb.LitColor);
			if (color != _mb.LitColor) {
				RecordUndo("Change Segment Lit Color", this);
				foreach (var mb in _mbs) {
					mb.UpdateColor(color);
				}
			}

			color = EditorGUILayout.ColorField("Unlit Color", _mb.UnlitColor);
			if (color != _mb.UnlitColor) {
				RecordUndo("Change Segment Unlit Color", this);
				foreach (var mb in _mbs) {
					mb.UnlitColor = color;
				}
			}
		}

		protected void RecordUndo(string description, DisplayInspector inspector)
		{
			var objs = _mbs
				.Select(mb => mb.GetComponent<MeshRenderer>().sharedMaterial as UnityEngine.Object)
				.Concat(_mbs.Select(mb => mb as UnityEngine.Object))
				.Concat(new UnityEngine.Object[] {inspector});
			Undo.RecordObjects(objs.ToArray(), description);
		}
	}
}
