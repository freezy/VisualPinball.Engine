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

using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemMovementInspector<TItem, TData, TMainAuthoring, TMovementAuthoring> : ItemInspector
		where TMovementAuthoring : ItemMovementAuthoring<TItem, TData, TMainAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		private TMovementAuthoring _movementAuthoring;

		protected TData Data => _movementAuthoring == null ? null : _movementAuthoring.Data;

		protected override void OnEnable()
		{
			_movementAuthoring = target as TMovementAuthoring;
			base.OnEnable();
		}

		protected void NoDataPanel()
		{
			// todo add more details
			GUILayout.Label("No data! Parent missing?");
		}
	}
}
