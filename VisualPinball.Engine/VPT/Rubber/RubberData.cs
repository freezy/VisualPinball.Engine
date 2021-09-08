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

#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Rubber
{
	[Serializable]
	public class RubberData : ItemData, IRubberData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 8)]
		public string Name = string.Empty;

		[BiffFloat("HTTP", Pos = 1)]
		public float Height { get; set; } = 25f;

		[BiffFloat("HTHI", Pos = 2)]
		public float HitHeight { get; set; } = 25f;

		[BiffInt("WDTP", Pos = 3)]
		public int Thickness { get; set; } = 8;

		[BiffBool("HTEV", Pos = 4)]
		public bool HitEvent = false;

		[BiffString("MATR", Pos = 5)]
		public string Material = string.Empty;

		[BiffString("IMAG", Pos = 9)]
		public string Image = string.Empty;

		[BiffFloat("ELAS", Pos = 10)]
		public float Elasticity;

		[BiffFloat("ELFO", Pos = 11)]
		public float ElasticityFalloff;

		[BiffFloat("RFCT", Pos = 12)]
		public float Friction;

		[BiffFloat("RSCT", Pos = 13)]
		public float Scatter;

		[BiffBool("CLDR", Pos = 14)]
		public bool IsCollidable = true;

		[BiffBool("RVIS", Pos = 15)]
		public bool IsVisible = true;

		[BiffBool("REEN", Pos = 21)]
		public bool IsReflectionEnabled = true;

		[BiffBool("ESTR", Pos = 16)]
		public bool StaticRendering = true;

		[BiffBool("ESIE", Pos = 17)]
		public bool ShowInEditor = true;

		[BiffFloat("ROTX", Pos = 18)]
		public float RotX { get; set; } = 0f;

		[BiffFloat("ROTY", Pos = 19)]
		public float RotY { get; set; } = 0f;

		[BiffFloat("ROTZ", Pos = 20)]
		public float RotZ { get; set; } = 0f;

		[BiffString("MAPH", Pos = 22)]
		public string PhysicsMaterial = string.Empty;

		[BiffBool("OVPH", Pos = 23)]
		public bool OverwritePhysics = false;

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints { get; set; }

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 7)]
		public int TimerInterval;

		[BiffTag("PNTS", Pos = 1999)]
		public bool Points;

		public RubberData() : base(StoragePrefix.GameItem)
		{
		}

		public RubberData(string name) : base(StoragePrefix.GameItem)
		{
			Name = name;
		}

		#region BIFF

		static RubberData()
		{
			Init(typeof(RubberData), Attributes);
		}

		public RubberData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Rubber);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
