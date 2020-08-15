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

		[BiffString("NAME", IsWideString = true, Pos = 15)]
		public string Name;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("RADI", Pos = 2)]
		public float Falloff = 50f;

		[BiffFloat("FAPO", Pos = 3)]
		public float FalloffPower = 2f;

		[BiffInt("STAT", Pos = 4)]
		public int State = LightStatus.LightStateOff;

		[BiffColor("COLR", Pos = 5)]
		public Color Color = new Color(0xffff00, ColorFormat.Argb);

		[BiffColor("COL2", Pos = 6)]
		public Color Color2 = new Color(0xffffff, ColorFormat.Argb);

		[BiffString("IMG1", Pos = 10)]
		public string OffImage = string.Empty;

		[BiffBool("SHAP", SkipWrite = true)]
		public bool IsRoundLight = false;

		[BiffString("BPAT", Pos = 9)]
		public string BlinkPattern = "10";

		[BiffInt("BINT", Pos = 11)]
		public int BlinkInterval = 125;

		[BiffFloat("BWTH", Pos = 12)]
		public float Intensity = 1f;

		[BiffFloat("TRMS", Pos = 13)]
		public float TransmissionScale = 0.5f;

		[BiffString("SURF", Pos = 14)]
		public string Surface = string.Empty;

		[BiffBool("BGLS", Pos = 16)]
		public bool IsBackglass = false;

		[BiffFloat("LIDB", Pos = 17)]
		public float DepthBias;

		[BiffFloat("FASP", Pos = 18)]
		public float FadeSpeedUp = 0.2f;

		[BiffFloat("FASD", Pos = 19)]
		public float FadeSpeedDown = 0.2f;

		[BiffBool("BULT", Pos = 20)]
		public bool IsBulbLight = false;

		[BiffBool("IMMO", Pos = 21)]
		public bool IsImageMode = false;

		[BiffBool("SHBM", Pos = 22)]
		public bool ShowBulbMesh = false;

		[BiffBool("STBM", Pos = 22)]
		public bool HasStaticBulbMesh = true;

		[BiffBool("SHRB", Pos = 23)]
		public bool ShowReflectionOnBall = true;

		[BiffFloat("BMSC", Pos = 24)]
		public float MeshRadius = 20f;

		[BiffFloat("BMVA", Pos = 25)]
		public float BulbModulateVsAdd = 0.9f;

		[BiffFloat("BHHI", Pos = 26)]
		public float BulbHaloHeight = 28f;

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;

		[BiffBool("TMON", Pos = 7)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 8)]
		public int TimerInterval;

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
