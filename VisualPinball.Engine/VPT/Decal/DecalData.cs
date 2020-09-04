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

namespace VisualPinball.Engine.VPT.Decal
{
	[Serializable]
	public class DecalData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 7)]
		public string Name;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("WDTH", Pos = 2)]
		public float Width = 100.0f;

		[BiffFloat("HIGH", Pos = 3)]
		public float Height = 100.0f;

		[BiffFloat("ROTA", Pos = 4)]
		public float Rotation = 0.0f;

		[BiffString("IMAG", Pos = 5)]
		public string Image;

		[BiffString("SURF", Pos = 6)]
		public string Surface;

		[BiffString("TEXT", Pos = 8)]
		public string Text;

		[BiffInt("TYPE", Pos = 9)]
		public int DecalType = VisualPinball.Engine.VPT.DecalType.DecalImage;

		[BiffInt("SIZE", Pos = 12)]
		public int SizingType = VisualPinball.Engine.VPT.SizingType.ManualSize;

		[BiffColor("COLR", Pos = 11)]
		public Color Color = new Color(0x000000, ColorFormat.Bgr);

		[BiffString("MATR", Pos = 10)]
		public string Material;

		[BiffBool("VERT", Pos = 13)]
		public bool VerticalText = false;

		[BiffBool("BGLS", Pos = 14)]
		public bool Backglass = false;

		[BiffFont("FONT", Pos = 2000)]
		public Font Font;

		#region BIFF

		static DecalData()
		{
			Init(typeof(DecalData), Attributes);
		}

		public DecalData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Decal);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
