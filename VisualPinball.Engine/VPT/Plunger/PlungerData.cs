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
		public string Name;

		[BiffInt("TYPE", Pos = 8)]
		public int Type = PlungerType.PlungerTypeModern;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("WDTH", Pos = 2)]
		public float Width = 25f;

		[BiffFloat("HIGH", Pos = 3)]
		public float Height = 20f;

		[BiffFloat("ZADJ", Pos = 4)]
		public float ZAdjust;

		[BiffFloat("HPSL", Pos = 5)]
		public float Stroke = 80f;

		[BiffFloat("SPDP", Pos = 6)]
		public float SpeedPull = 0.5f;

		[BiffFloat("SPDF", Pos = 7)]
		public float SpeedFire = 80f;

		[BiffFloat("MEST", Pos = 12)]
		public float MechStrength = 85f;

		[BiffFloat("MPRK", Pos = 15)]
		public float ParkPosition = 0.5f / 3.0f;

		[BiffFloat("PSCV", Pos = 16)]
		public float ScatterVelocity = 0f;

		[BiffFloat("MOMX", Pos = 17)]
		public float MomentumXfer = 1f;

		[BiffBool("MECH", Pos = 13)]
		public bool IsMechPlunger = false;

		[BiffBool("APLG", Pos = 14)]
		public bool AutoPlunger = false;

		[BiffInt("ANFR", Pos = 9)]
		public int AnimFrames = 1;

		[MaterialReference]
		[BiffString("MATR", Pos = 10)]
		public string Material = string.Empty;

		[TextureReference]
		[BiffString("IMAG", Pos = 11)]
		public string Image = string.Empty;

		[BiffBool("VSBL", Pos = 20)]
		public bool IsVisible = true;

		[BiffBool("REEN", Pos = 21)]
		public bool IsReflectionEnabled = true;

		[BiffString("SURF", Pos = 22)]
		public string Surface = string.Empty;

		[BiffString("TIPS", Pos = 24)]
		public string TipShape = "0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 14 .92; 39 .84";

		[BiffFloat("RODD", Pos = 25)]
		public float RodDiam = 0.6f;

		[BiffFloat("RNGG", Pos = 26)]
		public float RingGap = 2.0f;

		[BiffFloat("RNGD", Pos = 27)]
		public float RingDiam = 0.94f;

		[BiffFloat("RNGW", Pos = 28)]
		public float RingWidth = 3.0f;

		[BiffFloat("SPRD", Pos = 29)]
		public float SpringDiam = 0.77f;

		[BiffFloat("SPRG", Pos = 30)]
		public float SpringGauge = 1.38f;

		[BiffFloat("SPRL", Pos = 31)]
		public float SpringLoops = 8.0f;

		[BiffFloat("SPRE", Pos = 32)]
		public float SpringEndLoops = 2.5f;

		[BiffBool("TMON", Pos = 18)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 19)]
		public int TimerInterval;

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
