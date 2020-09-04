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

// ReSharper disable UnassignedField.Global

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
		public int EditorLayer;

		[BiffString("LANR", Pos = 1002)]
		public string EditorLayerName  = string.Empty;

		[BiffBool("LVIS", Pos = 1003)]
		public bool EditorLayerVisibility = true;

		public abstract string GetName();
		public abstract void SetName(string name);

		protected ItemData(StoragePrefix prefix) : base(prefix)
		{
		}

		protected ItemData(string storageName) : base(storageName)
		{
		}
	}

	public interface IPhysicalData
	{
		float GetElasticity();
		float GetElasticityFalloff();
		float GetFriction();
		float GetScatter();
		bool GetOverwritePhysics();
		bool GetIsCollidable();
		string GetPhysicsMaterial();
	}
}
