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

// ReSharper disable AssignmentInConditionalExpression

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemMeshInspector<TItem, TData, TMainAuthoring, TMeshAuthoring> : ItemInspector
		where TMeshAuthoring : ItemMeshAuthoring<TItem, TData, TMainAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		private TMeshAuthoring _meshAuthoring;

		protected TData Data => _meshAuthoring == null ? null : _meshAuthoring.Data;

		protected override void OnEnable()
		{
			_meshAuthoring = target as TMeshAuthoring;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			if (_meshAuthoring == null) {
				return;
			}

		}

		protected void NoDataPanel()
		{
			// todo add more details
			GUILayout.Label("No data! Parent missing?");
		}
	}
}
