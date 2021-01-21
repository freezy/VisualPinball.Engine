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
using RedBlackTree;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Mappings
{
	[Serializable]
	public class MappingsSwitchData : BiffData
	{
		[BiffString("MSID", IsWideString = true, Pos = 1)]
		public string Id = string.Empty;

		[BiffInt("MSII", Pos = 2)]
		public int InternalId;

		[BiffBool("MSNC", Pos = 3)]
		public bool IsNormallyClosed = false;

		[BiffString("DESC", IsWideString = true, Pos = 4)]
		public string Description = string.Empty;

		[BiffInt("SRCE", Pos = 5)]
		public int Source = SwitchSource.Playfield;

		[BiffString("INPM", IsWideString = true, Pos = 6)]
		public string InputActionMap = string.Empty;

		[BiffString("INPA", IsWideString = true, Pos = 7)]
		public string InputAction = string.Empty;

		[BiffString("PITM", IsWideString = true, Pos = 8)]
		public string PlayfieldItem = string.Empty;

		[BiffInt("CNST", Pos = 9)]
		public int Constant;

		[BiffString("DEVC", IsWideString = true, Pos = 10)]
		public string Device = string.Empty;

		[BiffString("DITM", IsWideString = true, Pos = 11)]
		public string DeviceItem = string.Empty;

		[BiffInt("STYP", Pos = 12)]
		public int Type = SwitchType.OnOff;

		[BiffInt("PLSE", Pos = 13)]
		public int PulseDelay = 250;

		#region BIFF

		static MappingsSwitchData()
		{
			Init(typeof(MappingsSwitchData), Attributes);
		}

		public MappingsSwitchData() : base(null)
		{
		}

		public MappingsSwitchData(BinaryReader reader) : base(null)
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
