#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flasher
{
	[Serializable]
	public class FlasherData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 10)]
		public string Name;

		[BiffFloat("FHEI", Pos = 1)]
		public float Height = 50.0f;

		[BiffFloat("FLAX", Pos = 2)] public float PosX { set => Center.X = value; get => Center.X; }
		[BiffFloat("FLAY", Pos = 3)] public float PosY { set => Center.Y = value; get => Center.Y; }
		public Vertex2D Center = new Vertex2D();

		[BiffFloat("FROX", Pos = 4)]
		public float RotX = 0.0f;

		[BiffFloat("FROY", Pos = 5)]
		public float RotY = 0.0f;

		[BiffFloat("FROZ", Pos = 6)]
		public float RotZ = 0.0f;

		[BiffColor("COLR", Pos = 7)]
		public Color Color = new Color(0xfffffff, ColorFormat.Bgr);

		[BiffString("IMAG", Pos = 11)]
		public string ImageA;

		[BiffString("IMAB", Pos = 12)]
		public string ImageB;

		[BiffInt("FALP", Min = 0, Pos = 13)]
		public int Alpha = 100;

		[BiffFloat("MOVA", Pos = 14)]
		public float ModulateVsAdd = 0.9f;

		[BiffBool("FVIS", Pos = 15)]
		public bool IsVisible = true;

		[BiffBool("ADDB", Pos = 17)]
		public bool AddBlend = false;

		[BiffBool("IDMD", Pos = 18)]
		public bool IsDmd = false;

		[BiffBool("DSPT", Pos = 16)]
		public bool DisplayTexture = false;

		[BiffFloat("FLDB", Pos = 19)]
		public float DepthBias = 0.0f;

		[BiffInt("ALGN", Pos = 20)]
		public int ImageAlignment = VisualPinball.Engine.VPT.ImageAlignment.ImageAlignTopLeft;

		[BiffInt("FILT", Pos = 21)]
		public int Filter = Filters.Filter_Overlay;

		[BiffInt("FIAM", Pos = 22)]
		public int FilterAmount = 100;

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;

		[BiffBool("TMON", Pos = 8)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 9)]
		public int TimerInterval;

		#region BIFF

		static FlasherData()
		{
			Init(typeof(FlasherData), Attributes);
		}

		public FlasherData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Flasher);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
