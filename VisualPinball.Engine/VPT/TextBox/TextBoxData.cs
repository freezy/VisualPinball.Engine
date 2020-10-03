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

namespace VisualPinball.Engine.VPT.TextBox
{
	[Serializable]
	public class TextBoxData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 9)]
		public string Name;

		[BiffVertex("VER1", Pos = 1)]
		public Vertex2D V1;

		[BiffVertex("VER2", Pos = 2)]
		public Vertex2D V2;

		[BiffColor("CLRB", Pos = 3)]
		public Color BackColor = new Color(0x000000f, ColorFormat.Bgr);

		[BiffColor("CLRF", Pos = 4)]
		public Color FontColor = new Color(0xfffffff, ColorFormat.Bgr);

		[BiffFloat("INSC", Pos = 5)]
		public float IntensityScale = 1.0f;

		[BiffString("TEXT", Pos = 6)]
		public string Text = "0";

		[BiffInt("ALGN", Pos = 10)]
		public int Align = TextAlignment.TextAlignRight;

		[BiffBool("TRNS", Pos = 11)]
		public bool IsTransparent = false;

		[BiffBool("IDMD", Pos = 12)]
		public bool IsDmd = false;

		[BiffFont("FONT", Pos = 2000)]
		public Font Font;

		[BiffBool("TMON", Pos = 7)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 8)]
		public int TimerInterval;

		#region BIFF

		static TextBoxData()
		{
			Init(typeof(TextBoxData), Attributes);
		}

		public TextBoxData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.TextBox);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
