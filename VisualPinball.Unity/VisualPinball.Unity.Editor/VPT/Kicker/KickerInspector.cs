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

// ReSharper disable AssignmentInConditionalExpression

using System.Collections.Generic;
using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerComponent)), CanEditMultipleObjects]
	public class KickerInspector : MainInspector<KickerData, KickerComponent>
	{
		private const string MeshFolder = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Kicker";

		private static readonly Dictionary<string, int> TypeMap = new Dictionary<string, int> {
			{ "Cup 1", KickerType.KickerCup },
			{ "Cup 2", KickerType.KickerCup2 },
			{ "Gottlieb", KickerType.KickerGottlieb },
			{ "Hole", KickerType.KickerHole },
			{ "Simple Hole", KickerType.KickerHoleSimple },
			{ "Williams", KickerType.KickerWilliams },
		};

		private SerializedProperty _positionProperty;
		private SerializedProperty _radiusProperty;
		private SerializedProperty _orientationProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _kickerTypeProperty;
		private SerializedProperty _meshNameProperty;
		private SerializedProperty _coilsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(KickerComponent.Position));
			_radiusProperty = serializedObject.FindProperty(nameof(KickerComponent.Radius));
			_orientationProperty = serializedObject.FindProperty(nameof(KickerComponent.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(KickerComponent._surface));
			_kickerTypeProperty = serializedObject.FindProperty(nameof(KickerComponent.KickerType));
			_meshNameProperty = serializedObject.FindProperty(nameof(KickerComponent.MeshName));
			_coilsProperty = serializedObject.FindProperty(nameof(KickerComponent.Coils));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_radiusProperty, updateTransforms: true);

			if (MainComponent.KickerType == KickerType.KickerCup ||
			    MainComponent.KickerType == KickerType.KickerWilliams) {
				PropertyField(_orientationProperty, updateTransforms: true);
			}
			PropertyField(_surfaceProperty, updateTransforms: true);

			MeshDropdownProperty("Mesh", _meshNameProperty, MeshFolder, MainComponent.gameObject, _kickerTypeProperty, TypeMap);

			PropertyField(_coilsProperty);

			base.OnInspectorGUI();

			EndEditing();
		}
	}
}
