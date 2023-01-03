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

// ReSharper disable AssignmentInConditionalExpression

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class MeshInspector<TData, TMainComponent, TMeshComponent> : ItemInspector
		where TMeshComponent : MeshComponent<TData, TMainComponent>
		where TData : ItemData
		where TMainComponent : MainRenderableComponent<TData>
	{
		protected TMeshComponent MeshComponent;

		protected override MonoBehaviour UndoTarget => MeshComponent.MainComponent;

		private bool HasMainComponent => MeshComponent == null || !MeshComponent.HasMainComponent;

		protected override void OnEnable()
		{
			MeshComponent = target as TMeshComponent;
			base.OnEnable();
		}

		public override void OnInspectorGUI()
		{
			if (MeshComponent == null) {
				return;
			}

			if (GUILayout.Button("Force Update Mesh")) {
				MeshComponent.RebuildMeshes();
			}
		}

		protected bool HasErrors()
		{
			if (HasMainComponent) {
				return false;
			}

			NoDataError();
			return true;
		}

		private static void NoDataError()
		{
			EditorGUILayout.HelpBox($"Cannot find main component!\n\nYou must have a {typeof(TMainComponent).Name} component on either this GameObject, its parent or grand parent.", MessageType.Error);
		}
	}
}
