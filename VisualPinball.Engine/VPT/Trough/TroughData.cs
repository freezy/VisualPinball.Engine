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

namespace VisualPinball.Engine.VPT.Trough
{
	[Serializable]
	public class TroughData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 9)]
		public string Name;

		[BiffVertex("ENTR", Pos = 1)]
		public Vertex2D Entrance;

		[BiffFloat("EXIT", Pos = 10)]
		public float ExitOffset = 200.0f;

		[BiffFloat("ORIN", Pos = 10)]
		public float Orientation = 0.0f;

		[BiffInt("BCNT", Pos = 3)]
		public int BallCount = 3;

		[BiffInt("SCNT", Pos = 4)]
		public int SwitchCount = 3;

		[BiffFloat("TIME", Pos = 5)]
		public float SettleTime = 0.1f;

		[BiffFloat("FORC", Pos = 6)]
		public float Force = 15f;

		[BiffFloat("BSCT", Pos = 7)]
		public float Scatter = 0f;

		[BiffString("SURF", Pos = 16)]
		public string Surface = string.Empty;

		#region BIFF

		static TroughData()
		{
			Init(typeof(TroughData), Attributes);
		}

		public TroughData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Trough);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
