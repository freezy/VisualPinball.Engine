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
// ReSharper disable UnusedAutoPropertyAccessor.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Kicker
{
	[Serializable]
	public class KickerData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 8)]
		public string Name = string.Empty;

		[BiffInt("TYPE", Pos = 9)]
		public int KickerType = VisualPinball.Engine.VPT.KickerType.KickerHole;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("RADI", Pos = 2)]
		public float Radius = 25f;

		[BiffFloat("KSCT", Pos = 10)]
		public float Scatter = 0.0f;

		[BiffFloat("KHAC", Pos = 11)]
		public float HitAccuracy = 0.7f;

		[BiffFloat("KHHI", Pos = 12)]
		public float HitHeight = 40.0f;

		[BiffFloat("KORI", Pos = 13)]
		public float Orientation = 0.0f;

		[BiffString("MATR", Pos = 5)]
		public string Material = string.Empty;

		[BiffString("SURF", Pos = 6)]
		public string Surface = string.Empty;

		[BiffBool("FATH", Pos = 14)]
		public bool FallThrough = false;

		[BiffBool("EBLD", Pos = 7)]
		public bool IsEnabled = true;

		[BiffBool("LEMO", Pos = 15)]
		public bool LegacyMode = true;

		[BiffBool("TMON", Pos = 3)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 4)]
		public int TimerInterval;

		// -----------------
		// new fields by VPE
		// -----------------

		[BiffFloat("ANGL", Pos = 16, SkipHash = true, IsVpeEnhancement = true)]
		public float Angle = 90f;

		[BiffFloat("SPED", Pos = 17, SkipHash = true, IsVpeEnhancement = true)]
		public float Speed = 3f;

		public KickerData() : base(StoragePrefix.GameItem)
		{
		}

		public KickerData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
		}

		#region BIFF

		static KickerData()
		{
			Init(typeof(KickerData), Attributes);
		}

		public KickerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Kicker);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
