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

namespace VisualPinball.Engine.VPT.Light
{
	[Serializable]
	[BiffIgnore("PNTS")]
	public class LightData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		//FP note: there are multiple types of "lights" in fp : playfield (shape and round) and bulbs

		[BiffString("NAME", IsWideString = true, Pos = 15)]
		public string Name = string.Empty;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;  // FP: yes position/glow_center (can be offset by script)

		[BiffFloat("RADI", Pos = 2)]
		public float Falloff = 50f;  // FP: yes (diameter, glow_radius)

		[BiffFloat("FAPO", Pos = 3)]
		public float FalloffPower = 2f;  // FP: no 

		[BiffInt("STAT", Pos = 4)]
		public int State = LightStatus.LightStateOff; // FP: yes

		[BiffColor("COLR", Pos = 5)]
		public Color Color = new Color(0xffff00, ColorFormat.Argb);  // FP: yes (lit/unlit color) 

		[BiffColor("COL2", Pos = 6)]
		public Color Color2 = new Color(0xffffff, ColorFormat.Argb); // FP: yes (lit/unlit color)

		[BiffString("IMG1", Pos = 10)]
		public string OffImage = string.Empty;  // FP: no 

		[BiffBool("SHAP", SkipWrite = true)]
		public bool IsRoundLight = false;  // FP: no (round shape lamp)

		[BiffString("BPAT", Pos = 9)]
		public string BlinkPattern = "10";  // FP: yes

		[BiffInt("BINT", Pos = 11)]
		public int BlinkInterval = 125;  // FP: yes

		[BiffFloat("BWTH", Pos = 12)]
		public float Intensity = 1f;  // FP: no 

		[BiffFloat("TRMS", Pos = 13)]
		public float TransmissionScale = 0.5f;  // FP: no 

		[BiffString("SURF", Pos = 14)]
		public string Surface = string.Empty;  // FP: yes

		[BiffBool("BGLS", Pos = 16)]
		public bool IsBackglass = false;   // FP: yes for bulbs

		[BiffFloat("LIDB", Pos = 17)]
		public float DepthBias;  // FP: no 

		[BiffFloat("FASP", Pos = 18)]
		public float FadeSpeedUp = 0.2f;  // FP: no 

		[BiffFloat("FASD", Pos = 19)]
		public float FadeSpeedDown = 0.2f;  // FP: no 

		[BiffBool("BULT", Pos = 20)]
		public bool IsBulbLight = false;  // FP: no (bulb type)

		[BiffBool("IMMO", Pos = 21)]
		public bool IsImageMode = false;  // FP: no 

		[BiffBool("SHBM", Pos = 22)]
		public bool ShowBulbMesh = false;  // FP: yes, for bulbs lights

		[BiffBool("STBM", Pos = 22)]
		public bool HasStaticBulbMesh = true;  // FP: idem 

		[BiffBool("SHRB", Pos = 23)]
		public bool ShowReflectionOnBall = true;  // FP: no 

		[BiffFloat("BMSC", Pos = 24)]
		public float MeshRadius = 20f;  // FP: no 

		[BiffFloat("BMVA", Pos = 25)]
		public float BulbModulateVsAdd = 0.9f;  // FP: no 

		[BiffFloat("BHHI", Pos = 26)]
		public float BulbHaloHeight = 28f;  // FP: no 

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;  // FP: no ? (shape lamp points?)

		[BiffBool("TMON", Pos = 7)]
		public bool IsTimerEnabled;  // FP: no 

		[BiffInt("TMIN", Pos = 8)]
		public int TimerInterval;  // FP: no 

		//FP +: cookie cut, lens texture, etc...
		#region BIFF

		static LightData()
		{
			Init(typeof(LightData), Attributes);
		}

		public LightData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public LightData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Light);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
