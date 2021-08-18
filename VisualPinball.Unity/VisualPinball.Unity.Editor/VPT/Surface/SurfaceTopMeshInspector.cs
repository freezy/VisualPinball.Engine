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

using UnityEditor;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceTopMeshAuthoring)), CanEditMultipleObjects]
	public class SurfaceTopMeshInspector : ItemMeshInspector<Surface, SurfaceData, SurfaceAuthoring, SurfaceTopMeshAuthoring>
	{
		private SurfaceData _data;

		protected override void OnEnable()
		{
			base.OnEnable();
			_data = Data;
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			EditorGUILayout.HelpBox($"This component computes the top mesh of the wall. It's parameterized by its parent.\n\nIf your wall serves as collider only, you can remove or deactivate this game object.", MessageType.Info);
		}
	}
}
