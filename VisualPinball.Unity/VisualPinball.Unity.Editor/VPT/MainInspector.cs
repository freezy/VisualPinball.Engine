﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	public class MainInspector<TData, TMainComponent> : ItemInspector
		where TData : ItemData
		where TMainComponent : MainComponent<TData>
	{
		protected TMainComponent MainComponent;

		protected override MonoBehaviour UndoTarget => MainComponent;

		protected override void OnEnable()
		{
			MainComponent = (TMainComponent)target;
			base.OnEnable();
		}

		protected bool HasErrors() => false;

		protected void UpdateSurfaceReferences(Transform obj)
		{
			var surfaceComponent = obj.gameObject.GetComponent<IOnSurfaceComponent>();
			if (surfaceComponent != null && surfaceComponent.Surface == MainComponent) {
				surfaceComponent.OnSurfaceUpdated();
			}
		}

		protected void UpdateTableHeightReferences(Transform obj)
		{
			var onPlayfieldComponent = obj.gameObject.GetComponent<IOnPlayfieldComponent>();
			onPlayfieldComponent?.OnPlayfieldHeightUpdated();
		}
	}
}
