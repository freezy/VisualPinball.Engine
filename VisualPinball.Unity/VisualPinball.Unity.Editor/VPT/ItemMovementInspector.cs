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
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class ItemMovementInspector<TItem, TData, TMainAuthoring, TMovementAuthoring> : ItemInspector
		where TMovementAuthoring : ItemMovementAuthoring<TItem, TData, TMainAuthoring>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		private TMovementAuthoring _movementAuthoring;

		protected TData Data => _movementAuthoring == null ? null : _movementAuthoring.Data;

		public override MonoBehaviour UndoTarget => _movementAuthoring.MainAuthoring;

		protected override void OnEnable()
		{
			_movementAuthoring = target as TMovementAuthoring;
			base.OnEnable();
		}

		protected bool HasErrors()
		{
			if (Data == null) {
				NoDataError();
				return true;
			}

			return false;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainAuthoring).Name} component on either this GameObject, its parent or grand parent.", MessageType.Error);
		}
	}
}
