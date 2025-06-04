// Visual Pinball Engine
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

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	[UxmlElement]
	public partial class LibraryAssetElement : VisualElement
	{
		public AssetResult Result;

		public const float RadiusRatio = 0.1f;

		private enum DragState
		{
			AtRest,
			Ready,
			Dragging
		}

		private DragState _dragState = DragState.AtRest;
		private Vector3 _mouseOffset;

		private IDragHandler _dragHandler;

		public LibraryAssetElement()
		{
			RegisterCallback<PointerDownEvent>(OnPointerDownEvent);
			RegisterCallback<PointerMoveEvent>(OnPointerMoveEvent);
			RegisterCallback<PointerUpEvent>(OnPointerUpEvent);
			RegisterCallback<MouseDownEvent>(OnMouseDown);
		}

		private void OnMouseDown(MouseDownEvent evt)
		{
			if (evt.clickCount == 2) {
				EditorGUIUtility.PingObject(Result.Asset.Object);
			}
		}

		public void SetSize(int size)
		{
			var img = this.Q<VisualElement>("thumbnail-mask");
			img.style.width = size;
			img.style.height = size;
			img.style.borderBottomLeftRadius = size * RadiusRatio;
			img.style.borderBottomRightRadius = size * RadiusRatio;
			img.style.borderTopLeftRadius = size * RadiusRatio;
			img.style.borderTopRightRadius = size * RadiusRatio;

			var label = this.Q<VisualElement>("label");
			label.style.maxWidth = size;
		}

		public void RegisterDrag(IDragHandler handler) => _dragHandler = handler;

		private void OnPointerDownEvent(PointerDownEvent evt)
		{
			if (/*evt.target == this && */evt.button == 0 && evt.isPrimary) {
				_dragState = DragState.Ready;
				_mouseOffset = evt.localPosition;
				this.CaptureMouse();
			}
		}

		private void OnPointerMoveEvent(PointerMoveEvent evt)
		{
			var movingDistance = (_mouseOffset - evt.localPosition).magnitude;
			if (_dragState == DragState.Ready && evt.pressedButtons == 1 && movingDistance > 25) {

				DragAndDrop.PrepareStartDrag();
				DragAndDrop.objectReferences = Array.Empty<Object>();
				DragAndDrop.paths = Array.Empty<string>();
				_dragHandler?.AttachData(Result);
				this.ReleaseMouse();
				DragAndDrop.StartDrag("Dragging..");
				_dragState = DragState.Dragging;
			}
		}

		private void OnPointerUpEvent(PointerUpEvent evt)
		{
			if (_dragState == DragState.Ready && evt.button == 0) {
				_dragState = DragState.AtRest;
			}
			this.ReleaseMouse();
			evt.StopImmediatePropagation();
		}
	}
}
