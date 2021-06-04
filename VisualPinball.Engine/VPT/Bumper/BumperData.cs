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

namespace VisualPinball.Engine.VPT.Bumper
{
	[Serializable]
	public class BumperData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 17)]
		public string Name = string.Empty;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;	// FP: idem

		[BiffFloat("RADI", Pos = 2)]
		public float Radius = 45f; // FP: No

		[MaterialReference]
		[BiffString("MATR", Pos = 12)]
		public string CapMaterial = string.Empty; // FP: cap texture, "crystal" for transparency

		[MaterialReference]
		[BiffString("RIMA", Pos = 15)]
		public string RingMaterial = string.Empty; // FP: No

		[MaterialReference]
		[BiffString("BAMA", Pos = 13)]
		public string BaseMaterial = string.Empty; // FP: Just color

		[MaterialReference]
		[BiffString("SKMA", Pos = 14)]
		public string SocketMaterial = string.Empty; //FP: Just color

		[BiffFloat("THRS", Pos = 5)]
		public float Threshold = 1.0f; // FP: No

		[BiffFloat("FORC", Pos = 6)]
		public float Force = 15f;	// FP: idem (strength)

		[BiffFloat("BSCT", Pos = 7)]
		public float Scatter;	// FP: No

		[BiffFloat("HISC", Pos = 8)]
		public float HeightScale = 90.0f; // FP: No

		[BiffFloat("RISP", Pos = 9)]
		public float RingSpeed = 0.5f;	// FP: No (animated in mesh)

		[BiffFloat("ORIN", Pos = 10)]
		public float Orientation = 0.0f;	// FP: yes (rotation)

		[BiffFloat("RDLI", Pos = 11)]
		public float RingDropOffset = 0.0f;	// FP: No (in animation)

		[BiffString("SURF", Pos = 16)]
		public string Surface = string.Empty;	// FP: yes

		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("CAVI", Pos = 18)]
		public bool IsCapVisible = true;	// FP: no (always cap)

		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("BSVS", Pos = 19)]
		public bool IsBaseVisible = true;	// FP: no (always base)

		[BiffBool("BSVS", SkipWrite = true)]
		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("RIVS", Pos = 20)]
		public bool IsRingVisible = true;	// FP: no (always ring)

		[BiffBool("BSVS", SkipWrite = true)]
		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("SKVS", Pos = 21)]
		public bool IsSocketVisible = true; // FP: yes (trigger_skirt)

		[BiffBool("HAHE", Pos = 22)]
		public bool HitEvent = true;	// FP: no?

		[BiffBool("COLI", Pos = 23)]
		public bool IsCollidable = true;	// FP: always

		[BiffBool("REEN", Pos = 24)]
		public bool IsReflectionEnabled = true; // FP: kind of? : "reflects_off_playfield"

		[BiffBool("TMON", Pos = 3)]
		public bool IsTimerEnabled;	// FP: no

		[BiffInt("TMIN", Pos = 4)]
		public int TimerInterval;	// FP: no

		// FP +: Lamp color, auto-flsh when hit, models...

		public BumperData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
		}

		#region BIFF

		static BumperData()
		{
			Init(typeof(BumperData), Attributes);
		}

		public BumperData(string storageName) : base(storageName)
		{
		}

		public BumperData(BinaryReader reader, string storageName) : this(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Bumper);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
		private static readonly List<BiffAttribute> IgnoredTags= new List<BiffAttribute>();

		#endregion
	}
}
