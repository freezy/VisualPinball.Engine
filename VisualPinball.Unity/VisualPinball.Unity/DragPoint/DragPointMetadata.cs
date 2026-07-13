// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	[Serializable]
	public class DragPointMetadata
	{
		[SerializeField] private bool _isSmooth;
		[SerializeField] private bool _isSlingshot;
		[SerializeField] private bool _hasAutoTexture = true;
		[SerializeField] private float _textureCoord;
		[SerializeField] private bool _isLocked;
		[SerializeField] private int _editorLayer;
		[SerializeField] private string _editorLayerName = string.Empty;
		[SerializeField] private bool _editorLayerVisibility = true;
		[SerializeField] private string _id;
		[SerializeField] private float _calcHeight;

		public bool IsSmooth { get => _isSmooth; set => _isSmooth = value; }
		public bool IsSlingshot { get => _isSlingshot; set => _isSlingshot = value; }
		public bool HasAutoTexture { get => _hasAutoTexture; set => _hasAutoTexture = value; }
		public float TextureCoord { get => _textureCoord; set => _textureCoord = value; }
		public bool IsLocked { get => _isLocked; set => _isLocked = value; }
		public int EditorLayer { get => _editorLayer; set => _editorLayer = value; }
		public string EditorLayerName { get => _editorLayerName; set => _editorLayerName = value; }
		public bool EditorLayerVisibility { get => _editorLayerVisibility; set => _editorLayerVisibility = value; }
		public string Id { get => _id; set => _id = value; }
		public float CalcHeight { get => _calcHeight; set => _calcHeight = value; }

		public DragPointMetadata() { }

		public DragPointMetadata(DragPointData dragPoint)
		{
			CopyFrom(dragPoint);
		}

		public static DragPointMetadata CreateInserted(DragPointMetadata previous,
			DragPointMetadata next)
		{
			return new DragPointMetadata {
				_isSmooth = next._isSmooth,
				_isSlingshot = next._isSlingshot,
				_hasAutoTexture = next._hasAutoTexture,
				_textureCoord = next._textureCoord,
				_editorLayer = next._editorLayer,
				_editorLayerName = next._editorLayerName,
				_editorLayerVisibility = previous._editorLayerVisibility,
				_id = Guid.NewGuid().ToString()[..8],
			};
		}

		public void CopyFrom(DragPointData dragPoint)
		{
			_isSmooth = dragPoint.IsSmooth;
			_isSlingshot = dragPoint.IsSlingshot;
			_hasAutoTexture = dragPoint.HasAutoTexture;
			_textureCoord = dragPoint.TextureCoord;
			_isLocked = dragPoint.IsLocked;
			_editorLayer = dragPoint.EditorLayer;
			_editorLayerName = dragPoint.EditorLayerName;
			_editorLayerVisibility = dragPoint.EditorLayerVisibility;
			_id = dragPoint.Id;
			_calcHeight = dragPoint.CalcHeight;
		}

		public void CopyTo(DragPointData dragPoint)
		{
			dragPoint.IsSmooth = _isSmooth;
			dragPoint.IsSlingshot = _isSlingshot;
			dragPoint.HasAutoTexture = _hasAutoTexture;
			dragPoint.TextureCoord = _textureCoord;
			dragPoint.IsLocked = _isLocked;
			dragPoint.EditorLayer = _editorLayer;
			dragPoint.EditorLayerName = _editorLayerName;
			dragPoint.EditorLayerVisibility = _editorLayerVisibility;
			dragPoint.Id = _id;
			dragPoint.CalcHeight = _calcHeight;
		}
	}
}
