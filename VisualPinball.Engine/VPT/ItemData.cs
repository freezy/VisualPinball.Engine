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

// ReSharper disable UnassignedField.Global
// ReSharper disable InconsistentNaming

using System;
using VisualPinball.Engine.IO;


namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// Data classes reflects what's in the VPX file.<p/>
	///
	/// Every playfield item has its own data class. They can currently
	/// only read data.
	/// </summary>
	[Serializable]
	public abstract class ItemData : BiffData
	{
		[BiffBool("LOCK", Pos = 1000)]
		public bool IsLocked;

		[BiffInt("LAYR", Pos = 1001)]
		public int EditorLayer {
			get => _editorLayer;
			set {
				_editorLayer = value;
				if (string.IsNullOrEmpty(EditorLayerName)) {
					EditorLayerName = $"Layer_{value + 1}";
				}
			}
		}

		[BiffString("LANR", Pos = 1002, WasAddedInVp107 = true)]
		public string EditorLayerName  = string.Empty;

		[BiffBool("LVIS", Pos = 1003, WasAddedInVp107 = true)]
		public bool EditorLayerVisibility = true;

		public virtual void FreeBinaryData()
		{
		}

		private int _editorLayer;

		public abstract string GetName();
		public abstract void SetName(string name);

		protected ItemData(StoragePrefix prefix) : base(prefix)
		{
		}

		protected ItemData(string storageName) : base(storageName)
		{
		}
	}
}
