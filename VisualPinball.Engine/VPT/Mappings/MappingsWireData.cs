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
	public class MappingsWireData : BiffData
	{
		[BiffString("DESC", IsWideString = true, Pos = 1)]
		public string Description = string.Empty;

		/* Source */

		[BiffInt("SRCE", Pos = 2)]
		public int Source = SwitchSource.Playfield;

		[BiffString("SIAM", IsWideString = true, Pos = 3)]
		public string SourceInputActionMap = string.Empty;

		[BiffString("SINA", IsWideString = true, Pos = 4)]
		public string SourceInputAction = string.Empty;

		[BiffString("SPIT", IsWideString = true, Pos = 5)]
		public string SourcePlayfieldItem = string.Empty;

		[BiffInt("SCON", Pos = 6)]
		public int SourceConstant;

		[BiffString("SDEV", IsWideString = true, Pos = 7)]
		public string SourceDevice = string.Empty;

		[BiffString("SDIT", IsWideString = true, Pos = 8)]
		public string SourceDeviceItem = string.Empty;

		/* Destination */

		[BiffInt("DEST", Pos = 9)]
		public int Destination = WireDestination.Playfield;

		[BiffString("DPIT", IsWideString = true, Pos = 10)]
		public string DestinationPlayfieldItem = string.Empty;

		[BiffString("DDEV", IsWideString = true, Pos = 11)]
		public string DestinationDevice = string.Empty;

		[BiffString("DDIT", IsWideString = true, Pos = 12)]
		public string DestinationDeviceItem = string.Empty;

		/* Type */

		[BiffInt("TYPE", Pos = 13)]
		public int Type = WireType.OnOff;

		[BiffInt("PLSE", Pos = 14)]
		public int Pulse = 10;

		#region BIFF

		static MappingsWireData()
		{
			Init(typeof(MappingsWireData), Attributes);
		}

		public MappingsWireData() : base(null)
		{
		}

		public MappingsWireData(BinaryReader reader) : base(null)
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
