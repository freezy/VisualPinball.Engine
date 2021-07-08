﻿// Visual Pinball Engine
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
	[CustomEditor(typeof(SurfaceSideMeshAuthoring))]
	public class SurfaceSideMeshInspector : ItemMeshInspector<Surface, SurfaceData, SurfaceAuthoring, SurfaceSideMeshAuthoring>
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

			TextureFieldLegacy("Side Texture", ref _data.SideImage);
			MaterialFieldLegacy("Side Material", ref _data.SideMaterial);

			base.OnInspectorGUI();
		}
	}
}
