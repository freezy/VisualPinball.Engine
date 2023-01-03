﻿// Visual Pinball Engine
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

// ReSharper disable CheckNamespace
// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(DotMatrixDisplayComponent)), CanEditMultipleObjects]
	public class DotMatrixDisplayInspector : DisplayInspector
	{
		[NonSerialized] private DotMatrixDisplayComponent _mb;
		[NonSerialized] private DotMatrixDisplayComponent[] _mbs;

		private new void OnEnable()
		{
			_mb = target as DotMatrixDisplayComponent;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			_mb.Id = EditorGUILayout.TextField("Id", _mb.Id);
			_mbs = targets.Select(t => t as DotMatrixDisplayComponent).ToArray();

			base.OnInspectorGUI();

			var width = EditorGUILayout.IntField("Columns", _mb.Width);
			if (width != _mb.Width) {
				_mb.Width = width;
			}

			var height = EditorGUILayout.IntField("Rows", _mb.Height);
			if (height != _mb.Height) {
				_mb.Height = height;
			}

			var padding = EditorGUILayout.Slider("Padding", _mb.Padding, 0.0f, 0.8f);
			if (padding != _mb.Padding) {
				RecordUndo("Change DMD Padding", this);
				foreach (var mb in _mbs) {
					mb.Padding = padding;
				}
			}

			var roundness = EditorGUILayout.Slider("Dot Roundness", _mb.Roundness, 0.0f, 0.5f);
			if (roundness != _mb.Roundness) {
				RecordUndo("Change DMD Dot Roundness", this);
				foreach (var mb in _mbs) {
					mb.Roundness = roundness;
				}
			}

			var emission = EditorGUILayout.Slider("Emission (nits)", _mb.Emission, 1f, 500f);
			if (emission != _mb.Emission) {
				RecordUndo("Change DMD Dot Emission", this);
				foreach (var mb in _mbs) {
					mb.Emission = emission;
				}
			}
		}

		[MenuItem("GameObject/Visual Pinball/Dot Matrix Display", false, 12)]
		private static void CreateDmdGameObject()
		{
			var go = new GameObject {
				name = "Dot Matrix Display"
			};

			if (Selection.activeGameObject != null) {
				go.transform.parent = Selection.activeGameObject.transform;

			} else {
				go.transform.localPosition = new Vector3(0f, 0.36f, 1.1f);
				go.transform.localScale = new Vector3(GameObjectScale, GameObjectScale, GameObjectScale);
			}

			var dmd = go.AddComponent<DotMatrixDisplayComponent>();
			dmd.UpdateDimensions(128, 32);

		}
	}
}
