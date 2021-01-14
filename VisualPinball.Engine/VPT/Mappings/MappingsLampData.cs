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
// ReSharper disable CompareOfFloatsByEqualityOperator

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Mappings
{
	[Serializable]
	public class MappingsLampData : BiffData
	{
		[BiffString("MCID", IsWideString = true, Pos = 1)]
		public string Id = string.Empty;

		[BiffInt("LSRC", Pos = 2)]
		public int Source = LampSource.Lamps;

		[BiffString("DESC", IsWideString = true, Pos = 3)]
		public string Description = string.Empty;

		[BiffInt("DEST", Pos = 4)]
		public int Destination = LampDestination.Playfield;

		[BiffString("PITM", IsWideString = true, Pos = 5)]
		public string PlayfieldItem = string.Empty;

		[BiffString("DEVC", IsWideString = true, Pos = 6)]
		public string Device = string.Empty;

		[BiffString("DITM", IsWideString = true, Pos = 7)]
		public string DeviceItem = string.Empty;

		[BiffInt("LTYP", Pos = 8)]
		public int Type = LampType.SingleOnOff;

		[BiffString("RGBG", IsWideString = true, Pos = 9)]
		public string Green = string.Empty;

		[BiffString("RGBB", IsWideString = true, Pos = 10)]
		public string Blue = string.Empty;

		#region BIFF

		static MappingsLampData()
		{
			Init(typeof(MappingsLampData), Attributes);
		}

		public MappingsLampData() : base(null)
		{
		}

		public MappingsLampData(BinaryReader reader) : base(null)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
