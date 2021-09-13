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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class AnimationInspector<TData, TMainComponent, TMovementComponent> : ItemInspector
		where TMovementComponent : AnimationComponent<TData, TMainComponent>
		where TData : ItemData
		where TMainComponent : MainRenderableComponent<TData>
	{
		private TMovementComponent _movementComponent;

		private bool HasMainComponent => _movementComponent == null || !_movementComponent.HasMainComponent;

		protected override MonoBehaviour UndoTarget => _movementComponent.MainComponent;

		protected override void OnEnable()
		{
			_movementComponent = target as TMovementComponent;
			base.OnEnable();
		}

		protected bool HasErrors()
		{
			if (!HasMainComponent) {
				NoDataError();
				return true;
			}

			return false;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainComponent).Name} component on either this GameObject, its parent or grand parent.", MessageType.Error);
		}
	}
}
