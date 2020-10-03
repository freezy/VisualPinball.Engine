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

namespace VisualPinball.Engine.VPT.MappingConfig
{
	[Serializable]
	public class MappingEntryData : BiffData
	{
		[BiffString("MPID", IsWideString = true, Pos = 1)]
		public string Id = string.Empty;

		[BiffString("DESC", IsWideString = true, Pos = 2)]
		public string Description = string.Empty;

		[BiffInt("SSRC", Pos = 3)]
		public int Source = SwitchSource.Playfield;

		[BiffString("INPM", IsWideString = true, Pos = 4)]
		public string InputActionMap = string.Empty;

		[BiffString("INPA", IsWideString = true, Pos = 5)]
		public string InputAction = string.Empty;

		[BiffString("PITM", IsWideString = true, Pos = 6)]
		public string PlayfieldItem = string.Empty;

		[BiffInt("CNST", Pos = 7)]
		public int Constant;

		[BiffInt("STYP", Pos = 8)]
		public int Type = SwitchType.OnOff;

		[BiffInt("PLSE", Pos = 9)]
		public int Pulse = 10;

		#region BIFF

		static MappingEntryData()
		{
			Init(typeof(MappingEntryData), Attributes);
		}

		public MappingEntryData() : base(null)
		{
		}

		public MappingEntryData(BinaryReader reader) : base(null)
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
