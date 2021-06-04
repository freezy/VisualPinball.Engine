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
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Plunger
{
	[Serializable]
	public class PlungerData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 23)]
		public string Name = string.Empty;

		[BiffInt("TYPE", Pos = 8)]
		public int Type = PlungerType.PlungerTypeModern;  // FP: no (auto or plunger)

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;  // FP: yes ( position)

		[BiffFloat("WDTH", Pos = 2)]
		public float Width = 25f;  // FP: no 

		[BiffFloat("HIGH", Pos = 3)]
		public float Height = 20f;  // FP: no 

		[BiffFloat("ZADJ", Pos = 4)]
		public float ZAdjust;  // FP: no 

		[BiffFloat("HPSL", Pos = 5)]
		public float Stroke = 80f;  // FP: no 

		[BiffFloat("SPDP", Pos = 6)]
		public float SpeedPull = 0.5f;  // FP: no 

		[BiffFloat("SPDF", Pos = 7)]
		public float SpeedFire = 80f;  // FP: no 

		[BiffFloat("MEST", Pos = 12)]
		public float MechStrength = 85f;  // FP: no 

		[BiffFloat("MPRK", Pos = 15)]
		public float ParkPosition = 0.5f / 3.0f;  // FP: no 

		[BiffFloat("PSCV", Pos = 16)]
		public float ScatterVelocity = 0f;  // FP: no 

		[BiffFloat("MOMX", Pos = 17)]
		public float MomentumXfer = 1f;  // FP: no 

		[BiffBool("MECH", Pos = 13)]
		public bool IsMechPlunger = false;  // FP: no 

		[BiffBool("APLG", Pos = 14)]
		public bool AutoPlunger = false;  // FP: no (other type)

		[BiffInt("ANFR", Pos = 9)]
		public int AnimFrames = 1;  // FP: no 

		[MaterialReference]
		[BiffString("MATR", Pos = 10)]
		public string Material = string.Empty;  // FP: no 

		[TextureReference]
		[BiffString("IMAG", Pos = 11)]
		public string Image = string.Empty;  // FP: yes (texture)

		[BiffBool("VSBL", Pos = 20)]
		public bool IsVisible = true;  // FP: no (model or not)

		[BiffBool("REEN", Pos = 21)]
		public bool IsReflectionEnabled = true;  // FP: yes (reflects off playfield)

		[BiffString("SURF", Pos = 22)]
		public string Surface = string.Empty;  // FP: no 

		[BiffString("TIPS", Pos = 24)]
		public string TipShape = "0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 14 .92; 39 .84";  // FP: no 

		[BiffFloat("RODD", Pos = 25)]
		public float RodDiam = 0.6f;  // FP: no 

		[BiffFloat("RNGG", Pos = 26)]
		public float RingGap = 2.0f;  // FP: no 

		[BiffFloat("RNGD", Pos = 27)]
		public float RingDiam = 0.94f;  // FP: no 

		[BiffFloat("RNGW", Pos = 28)]
		public float RingWidth = 3.0f;  // FP: no 

		[BiffFloat("SPRD", Pos = 29)]
		public float SpringDiam = 0.77f;  // FP: no 

		[BiffFloat("SPRG", Pos = 30)]
		public float SpringGauge = 1.38f;  // FP: no 

		[BiffFloat("SPRL", Pos = 31)]
		public float SpringLoops = 8.0f;  // FP: no 

		[BiffFloat("SPRE", Pos = 32)]
		public float SpringEndLoops = 2.5f;  // FP: no 

		[BiffBool("TMON", Pos = 18)]
		public bool IsTimerEnabled;  // FP: no 

		[BiffInt("TMIN", Pos = 19)]
		public int TimerInterval;  // FP: no 

		// FP+: strength, model, plunger_color, face_plate_color, Vcuts params...

		public Color Color = new Color(0x4c4c4cf, ColorFormat.Bgr);

		public PlungerData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
		}

		#region BIFF

		static PlungerData()
		{
			Init(typeof(PlungerData), Attributes);
		}

		public PlungerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Plunger);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
